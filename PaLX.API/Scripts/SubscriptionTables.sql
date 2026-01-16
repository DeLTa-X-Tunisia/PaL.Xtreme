-- ============================================
-- PaL.Xtreme - Tables de Gestion des Abonnements
-- ============================================

-- ============================================
-- 1. Table des Tiers d'abonnement Utilisateur
-- ============================================
CREATE TABLE IF NOT EXISTS "SubscriptionTiers" (
    "Id" SERIAL PRIMARY KEY,
    "Tier" INT UNIQUE NOT NULL,
    "Name" VARCHAR(50) NOT NULL,
    "DisplayName" VARCHAR(100) NOT NULL,
    "Description" TEXT,
    "Color" VARCHAR(7) DEFAULT '#808080',
    "Icon" VARCHAR(50) DEFAULT 'user',
    "BadgePath" VARCHAR(255),
    
    -- Limites
    "MaxRooms" INT DEFAULT 1,
    "MaxRoomCapacity" INT DEFAULT 20,
    
    -- Fonctionnalités
    "CanAccess18Plus" BOOLEAN DEFAULT FALSE,
    "HasBadge" BOOLEAN DEFAULT FALSE,
    "CustomPseudoColor" BOOLEAN DEFAULT FALSE,
    "NoAds" BOOLEAN DEFAULT FALSE,
    "PriorityQueue" BOOLEAN DEFAULT FALSE,
    "EarlyAccess" BOOLEAN DEFAULT FALSE,
    
    -- Prix de BASE par jour (en centimes) - les durées appliquent des remises
    "BasePricePerDayCents" INT DEFAULT 0,
    "BasePointsPerDay" INT DEFAULT 0,
    
    "IsAvailable" BOOLEAN DEFAULT TRUE,
    "SortOrder" INT DEFAULT 0,
    "CreatedAt" TIMESTAMP DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP DEFAULT NOW()
);

-- Insertion des tiers par défaut
INSERT INTO "SubscriptionTiers" ("Tier", "Name", "DisplayName", "Description", "Color", "Icon", "MaxRooms", "MaxRoomCapacity", "CanAccess18Plus", "HasBadge", "CustomPseudoColor", "NoAds", "PriorityQueue", "EarlyAccess", "BasePricePerDayCents", "BasePointsPerDay", "SortOrder")
VALUES 
    (0, 'Member', 'Membre', 'Compte gratuit avec fonctionnalités de base', '#95A5A6', 'user', 1, 20, FALSE, FALSE, FALSE, FALSE, FALSE, FALSE, 0, 0, 0),
    (1, 'Deluxe', 'Deluxe', 'Sans publicité, plus de salons', '#3498DB', 'star', 3, 50, FALSE, TRUE, FALSE, TRUE, FALSE, FALSE, 30, 30, 1),
    (2, 'VIP', 'VIP', 'Accès 18+, priorité, badge animé', '#F1C40F', 'crown', 5, 100, TRUE, TRUE, FALSE, TRUE, TRUE, FALSE, 50, 50, 2),
    (3, 'Royal', 'Royal', 'Couleur pseudo personnalisée, salons premium', '#9B59B6', 'gem', 10, 200, TRUE, TRUE, TRUE, TRUE, TRUE, FALSE, 80, 80, 3),
    (4, 'Legend', 'Légende', 'Tout illimité + accès anticipé', '#E74C3C', 'fire', 999, 500, TRUE, TRUE, TRUE, TRUE, TRUE, TRUE, 120, 120, 4)
ON CONFLICT ("Tier") DO NOTHING;

-- ============================================
-- 2. Table des Durées d'abonnement
-- ============================================
CREATE TABLE IF NOT EXISTS "SubscriptionDurations" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(50) NOT NULL,
    "DisplayName" VARCHAR(100) NOT NULL,
    "BaseDays" INT NOT NULL,
    "BonusDays" INT DEFAULT 0,
    "TotalDays" INT GENERATED ALWAYS AS ("BaseDays" + "BonusDays") STORED,
    "DiscountPercent" INT DEFAULT 0,
    "IsAvailable" BOOLEAN DEFAULT TRUE,
    "SortOrder" INT DEFAULT 0,
    "CreatedAt" TIMESTAMP DEFAULT NOW()
);

-- Insertion des durées par défaut
INSERT INTO "SubscriptionDurations" ("Name", "DisplayName", "BaseDays", "BonusDays", "DiscountPercent", "SortOrder")
VALUES 
    ('1_day', '1 jour', 1, 0, 0, 1),
    ('2_days', '2 jours', 2, 0, 0, 2),
    ('3_days', '3 jours', 3, 0, 5, 3),
    ('1_week', '1 semaine', 7, 1, 10, 4),
    ('1_month', '1 mois', 30, 3, 15, 5),
    ('3_months', '3 mois', 90, 7, 20, 6),
    ('6_months', '6 mois', 180, 14, 25, 7),
    ('1_year', '1 an', 365, 30, 30, 8),
    ('2_years', '2 ans', 730, 90, 40, 9)
ON CONFLICT DO NOTHING;

-- ============================================
-- 3. Table des Prix par Tier et Durée
-- (Permet de personnaliser le prix de chaque combinaison)
-- ============================================
CREATE TABLE IF NOT EXISTS "SubscriptionPrices" (
    "Id" SERIAL PRIMARY KEY,
    "TierId" INT NOT NULL REFERENCES "SubscriptionTiers"("Id") ON DELETE CASCADE,
    "DurationId" INT NOT NULL REFERENCES "SubscriptionDurations"("Id") ON DELETE CASCADE,
    "PriceCents" INT NOT NULL DEFAULT 0,
    "Points" INT NOT NULL DEFAULT 0,
    "IsCustomPrice" BOOLEAN DEFAULT FALSE, -- TRUE si prix personnalisé, sinon calculé
    "CreatedAt" TIMESTAMP DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP DEFAULT NOW(),
    UNIQUE("TierId", "DurationId")
);

-- ============================================
-- 4. Table des Abonnements Utilisateurs (améliorée)
-- ============================================
-- Ajouter les colonnes manquantes à UserSubscriptions si elles n'existent pas
DO $$
BEGIN
    -- Ajouter TierId si n'existe pas
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UserSubscriptions' AND column_name = 'TierId') THEN
        ALTER TABLE "UserSubscriptions" ADD COLUMN "TierId" INT REFERENCES "SubscriptionTiers"("Id");
    END IF;
    
    -- Ajouter IsActive si n'existe pas
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UserSubscriptions' AND column_name = 'IsActive') THEN
        ALTER TABLE "UserSubscriptions" ADD COLUMN "IsActive" BOOLEAN DEFAULT TRUE;
    END IF;
    
    -- Ajouter ExpiresAt si n'existe pas
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UserSubscriptions' AND column_name = 'ExpiresAt') THEN
        ALTER TABLE "UserSubscriptions" ADD COLUMN "ExpiresAt" TIMESTAMP;
    END IF;
    
    -- Ajouter GrantedByAdminId si n'existe pas
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UserSubscriptions' AND column_name = 'GrantedByAdminId') THEN
        ALTER TABLE "UserSubscriptions" ADD COLUMN "GrantedByAdminId" INT REFERENCES "Users"("Id");
    END IF;
    
    -- Ajouter PaymentMethod si n'existe pas
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UserSubscriptions' AND column_name = 'PaymentMethod') THEN
        ALTER TABLE "UserSubscriptions" ADD COLUMN "PaymentMethod" VARCHAR(20) DEFAULT 'admin'; -- admin, points, stripe
    END IF;
    
    -- Ajouter PricePaid si n'existe pas
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UserSubscriptions' AND column_name = 'PricePaid') THEN
        ALTER TABLE "UserSubscriptions" ADD COLUMN "PricePaid" INT DEFAULT 0;
    END IF;
    
    -- Ajouter PointsUsed si n'existe pas
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UserSubscriptions' AND column_name = 'PointsUsed') THEN
        ALTER TABLE "UserSubscriptions" ADD COLUMN "PointsUsed" INT DEFAULT 0;
    END IF;
    
    -- Ajouter IsTrial si n'existe pas
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'UserSubscriptions' AND column_name = 'IsTrial') THEN
        ALTER TABLE "UserSubscriptions" ADD COLUMN "IsTrial" BOOLEAN DEFAULT FALSE;
    END IF;
END $$;

-- ============================================
-- 5. Table du Solde de Points Utilisateur
-- ============================================
CREATE TABLE IF NOT EXISTS "UserPoints" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Balance" INT NOT NULL DEFAULT 0,
    "TotalEarned" INT NOT NULL DEFAULT 0,
    "TotalSpent" INT NOT NULL DEFAULT 0,
    "UpdatedAt" TIMESTAMP DEFAULT NOW(),
    UNIQUE("UserId")
);

-- ============================================
-- 6. Table des Transactions de Points
-- ============================================
CREATE TABLE IF NOT EXISTS "PointTransactions" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Amount" INT NOT NULL, -- Positif = gain, Négatif = dépense
    "Type" VARCHAR(30) NOT NULL, -- purchase, subscription, gift, bonus, refund, admin_grant
    "Description" TEXT,
    "ReferenceId" INT, -- ID de l'abonnement ou achat associé
    "BalanceBefore" INT NOT NULL,
    "BalanceAfter" INT NOT NULL,
    "CreatedAt" TIMESTAMP DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_pointtransactions_userid ON "PointTransactions"("UserId");
CREATE INDEX IF NOT EXISTS idx_pointtransactions_createdat ON "PointTransactions"("CreatedAt" DESC);

-- ============================================
-- 7. Table des Périodes d'Essai Utilisées
-- ============================================
CREATE TABLE IF NOT EXISTS "UsedTrials" (
    "Id" SERIAL PRIMARY KEY,
    "UserId" INT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "TierId" INT NOT NULL REFERENCES "SubscriptionTiers"("Id"),
    "UsedAt" TIMESTAMP DEFAULT NOW(),
    UNIQUE("UserId", "TierId")
);

-- ============================================
-- 8. Index pour les performances
-- ============================================
CREATE INDEX IF NOT EXISTS idx_subscriptiontiers_tier ON "SubscriptionTiers"("Tier");
CREATE INDEX IF NOT EXISTS idx_subscriptionprices_tierid ON "SubscriptionPrices"("TierId");
CREATE INDEX IF NOT EXISTS idx_usersubscriptions_active ON "UserSubscriptions"("UserId", "IsActive");
CREATE INDEX IF NOT EXISTS idx_userpoints_userid ON "UserPoints"("UserId");

-- ============================================
-- 9. Fonction pour calculer le prix
-- ============================================
CREATE OR REPLACE FUNCTION calculate_subscription_price(
    p_tier_id INT,
    p_duration_id INT
) RETURNS TABLE(price_cents INT, points INT) AS $$
DECLARE
    v_base_price INT;
    v_base_points INT;
    v_base_days INT;
    v_discount INT;
    v_custom_price RECORD;
BEGIN
    -- Vérifier s'il existe un prix personnalisé
    SELECT sp."PriceCents", sp."Points", sp."IsCustomPrice"
    INTO v_custom_price
    FROM "SubscriptionPrices" sp
    WHERE sp."TierId" = p_tier_id AND sp."DurationId" = p_duration_id;
    
    IF FOUND AND v_custom_price."IsCustomPrice" THEN
        RETURN QUERY SELECT v_custom_price."PriceCents", v_custom_price."Points";
        RETURN;
    END IF;
    
    -- Sinon calculer le prix
    SELECT st."BasePricePerDayCents", st."BasePointsPerDay"
    INTO v_base_price, v_base_points
    FROM "SubscriptionTiers" st
    WHERE st."Id" = p_tier_id;
    
    SELECT sd."BaseDays", sd."DiscountPercent"
    INTO v_base_days, v_discount
    FROM "SubscriptionDurations" sd
    WHERE sd."Id" = p_duration_id;
    
    -- Calcul: prix_base * jours * (1 - remise/100)
    price_cents := ROUND(v_base_price * v_base_days * (100 - v_discount) / 100.0)::INT;
    points := ROUND(v_base_points * v_base_days * (100 - v_discount) / 100.0)::INT;
    
    RETURN QUERY SELECT price_cents, points;
END;
$$ LANGUAGE plpgsql;

-- ============================================
-- COMMENTAIRES
-- ============================================
COMMENT ON TABLE "SubscriptionTiers" IS 'Niveaux d''abonnement (Gratuit, Deluxe, VIP, Royal, Legend)';
COMMENT ON TABLE "SubscriptionDurations" IS 'Durées disponibles avec bonus et remises';
COMMENT ON TABLE "SubscriptionPrices" IS 'Prix personnalisés par combinaison tier+durée';
COMMENT ON TABLE "UserPoints" IS 'Solde de points des utilisateurs';
COMMENT ON TABLE "PointTransactions" IS 'Historique des transactions de points';
COMMENT ON TABLE "UsedTrials" IS 'Suivi des périodes d''essai utilisées';
