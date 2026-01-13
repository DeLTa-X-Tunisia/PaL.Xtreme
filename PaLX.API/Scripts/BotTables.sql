-- Tables pour le système de Bot IA
-- À exécuter sur PostgreSQL

-- Table de configuration du Bot par salon
CREATE TABLE IF NOT EXISTS BotConfigs (
    Id SERIAL PRIMARY KEY,
    RoomId INTEGER NOT NULL REFERENCES Rooms(Id) ON DELETE CASCADE,
    BotName VARCHAR(50) NOT NULL DEFAULT 'PaLX Bot',
    BotAvatarUrl VARCHAR(255) DEFAULT '/images/bot-avatar.png',
    
    -- Fonctionnalités activées
    IsEnabled BOOLEAN NOT NULL DEFAULT TRUE,
    WelcomeMessageEnabled BOOLEAN NOT NULL DEFAULT TRUE,
    ModerationEnabled BOOLEAN NOT NULL DEFAULT TRUE,
    QuizEnabled BOOLEAN NOT NULL DEFAULT FALSE,
    MentionResponseEnabled BOOLEAN NOT NULL DEFAULT TRUE,
    TopicSuggestionEnabled BOOLEAN NOT NULL DEFAULT FALSE,
    
    -- Messages personnalisés
    WelcomeMessageTemplate TEXT DEFAULT 'Bienvenue {username} dans le salon ! 👋',
    WarningMessageTemplate TEXT DEFAULT '⚠️ {username}, merci de respecter les règles du salon.',
    KickMessageTemplate TEXT DEFAULT '❌ {username} a été expulsé pour comportement inapproprié.',
    
    -- Paramètres de modération
    WarningsBeforeKick INTEGER NOT NULL DEFAULT 3,
    WarningResetMinutes INTEGER NOT NULL DEFAULT 60,
    
    -- Paramètres Quiz
    QuizIntervalMinutes INTEGER NOT NULL DEFAULT 30,
    QuizTimeoutSeconds INTEGER NOT NULL DEFAULT 60,
    
    -- Timestamps
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT unique_room_bot UNIQUE (RoomId)
);

-- Table des avertissements donnés par le bot
CREATE TABLE IF NOT EXISTS BotWarnings (
    Id SERIAL PRIMARY KEY,
    RoomId INTEGER NOT NULL REFERENCES Rooms(Id) ON DELETE CASCADE,
    UserId INTEGER NOT NULL REFERENCES Users(Id) ON DELETE CASCADE,
    Reason VARCHAR(500) NOT NULL DEFAULT '',
    TriggerWord VARCHAR(100) NOT NULL DEFAULT '',
    OriginalMessage TEXT NOT NULL DEFAULT '',
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

-- Index pour rechercher rapidement les warnings actifs d'un utilisateur
CREATE INDEX IF NOT EXISTS idx_botwarnings_room_user_active 
ON BotWarnings(RoomId, UserId, IsActive) 
WHERE IsActive = TRUE;

-- Table des mots interdits par salon
CREATE TABLE IF NOT EXISTS BannedWords (
    Id SERIAL PRIMARY KEY,
    RoomId INTEGER NOT NULL REFERENCES Rooms(Id) ON DELETE CASCADE,
    Word VARCHAR(100) NOT NULL,
    Severity VARCHAR(20) NOT NULL DEFAULT 'Warning', -- Warning, Kick, Ban
    AddedBy INTEGER NOT NULL REFERENCES Users(Id) ON DELETE SET NULL,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT unique_room_word UNIQUE (RoomId, Word)
);

-- Index pour recherche rapide des mots interdits
CREATE INDEX IF NOT EXISTS idx_bannedwords_room ON BannedWords(RoomId);

-- Table des questions de quiz
CREATE TABLE IF NOT EXISTS QuizQuestions (
    Id SERIAL PRIMARY KEY,
    RoomId INTEGER DEFAULT 0, -- 0 = question globale disponible pour tous
    Question TEXT NOT NULL,
    Answer VARCHAR(500) NOT NULL,
    Options TEXT[], -- Array de chaînes pour QCM
    Category VARCHAR(50) NOT NULL DEFAULT 'General',
    Points INTEGER NOT NULL DEFAULT 10,
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Index pour les questions par catégorie
CREATE INDEX IF NOT EXISTS idx_quizquestions_category ON QuizQuestions(Category);
CREATE INDEX IF NOT EXISTS idx_quizquestions_room ON QuizQuestions(RoomId);

-- Table des sujets de discussion
CREATE TABLE IF NOT EXISTS DiscussionTopics (
    Id SERIAL PRIMARY KEY,
    RoomId INTEGER DEFAULT 0, -- 0 = topic global
    Topic TEXT NOT NULL,
    Category VARCHAR(50) NOT NULL DEFAULT 'General',
    CreatedAt TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Index pour les topics par catégorie
CREATE INDEX IF NOT EXISTS idx_discussiontopics_category ON DiscussionTopics(Category);

-- Quelques questions de quiz par défaut
INSERT INTO QuizQuestions (RoomId, Question, Answer, Options, Category, Points) VALUES
(0, 'Quelle est la capitale de la France ?', 'Paris', ARRAY['Lyon', 'Paris', 'Marseille', 'Bordeaux'], 'Géographie', 10),
(0, 'Combien de continents y a-t-il sur Terre ?', '7', ARRAY['5', '6', '7', '8'], 'Géographie', 10),
(0, 'Quel est le plus grand océan du monde ?', 'Pacifique', ARRAY['Atlantique', 'Indien', 'Pacifique', 'Arctique'], 'Géographie', 15),
(0, 'Qui a peint la Joconde ?', 'Léonard de Vinci', ARRAY['Michel-Ange', 'Raphaël', 'Léonard de Vinci', 'Botticelli'], 'Art', 10),
(0, 'En quelle année l''homme a-t-il marché sur la Lune pour la première fois ?', '1969', ARRAY['1965', '1969', '1972', '1975'], 'Histoire', 15),
(0, 'Quel est le symbole chimique de l''or ?', 'Au', ARRAY['Or', 'Au', 'Ag', 'Go'], 'Science', 10),
(0, 'Combien de joueurs composent une équipe de football sur le terrain ?', '11', ARRAY['9', '10', '11', '12'], 'Sport', 10),
(0, 'Quelle planète est surnommée la planète rouge ?', 'Mars', ARRAY['Vénus', 'Mars', 'Jupiter', 'Saturne'], 'Science', 10),
(0, 'Quel est le plus long fleuve du monde ?', 'Nil', ARRAY['Amazone', 'Nil', 'Yangtsé', 'Mississippi'], 'Géographie', 15),
(0, 'Qui a écrit "Les Misérables" ?', 'Victor Hugo', ARRAY['Émile Zola', 'Victor Hugo', 'Gustave Flaubert', 'Alexandre Dumas'], 'Littérature', 10)
ON CONFLICT DO NOTHING;

-- Quelques sujets de discussion par défaut
INSERT INTO DiscussionTopics (RoomId, Topic, Category) VALUES
(0, 'Si vous pouviez voyager dans le temps, quelle époque visiteriez-vous ?', 'Philosophie'),
(0, 'Quel est le film qui vous a le plus marqué et pourquoi ?', 'Cinéma'),
(0, 'Quelle serait votre superpuissance idéale ?', 'Fun'),
(0, 'Si vous pouviez dîner avec une personnalité historique, qui choisiriez-vous ?', 'Histoire'),
(0, 'Quel est le plus beau pays que vous avez visité ?', 'Voyages'),
(0, 'Quelle est la musique qui vous met toujours de bonne humeur ?', 'Musique'),
(0, 'Quel conseil donneriez-vous à votre vous de 18 ans ?', 'Philosophie'),
(0, 'Quel est votre plat préféré et savez-vous le cuisiner ?', 'Cuisine'),
(0, 'Quelle est la série TV que vous recommanderiez absolument ?', 'Séries'),
(0, 'Si vous gagniez au loto demain, que feriez-vous en premier ?', 'Fun')
ON CONFLICT DO NOTHING;

-- Mots interdits globaux (exemple - à adapter selon vos besoins)
-- Note: Ces mots sont des exemples, vous devrez les adapter
-- INSERT INTO BannedWords (RoomId, Word, Severity, AddedBy) VALUES
-- (room_id, 'mot_inapproprié', 'Warning', admin_user_id);

-- Fonction pour reset automatique des warnings expirés
CREATE OR REPLACE FUNCTION reset_expired_warnings() RETURNS void AS $$
BEGIN
    UPDATE BotWarnings bw
    SET IsActive = FALSE
    FROM BotConfigs bc
    WHERE bw.RoomId = bc.RoomId
    AND bw.IsActive = TRUE
    AND bw.CreatedAt < NOW() - (bc.WarningResetMinutes || ' minutes')::INTERVAL;
END;
$$ LANGUAGE plpgsql;

-- Commentaire: Vous pouvez créer un job cron pour appeler cette fonction périodiquement
-- Exemple: SELECT reset_expired_warnings(); toutes les 5 minutes
