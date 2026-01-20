-- v2.4.5: Script de migration pour réinitialiser IsMuted à FALSE pour tous les membres
-- Exécuter ce script une seule fois après la mise à jour vers v2.4.5

-- Réinitialiser IsMuted = FALSE pour tous les membres de room
UPDATE "RoomMembers" SET "IsMuted" = FALSE;

-- Vérifier le résultat
SELECT COUNT(*) as "Total Members", 
       SUM(CASE WHEN "IsMuted" = TRUE THEN 1 ELSE 0 END) as "Muted Count",
       SUM(CASE WHEN "IsMuted" = FALSE THEN 1 ELSE 0 END) as "Unmuted Count"
FROM "RoomMembers";

-- Optionnel: S'assurer que la valeur par défaut de la colonne est FALSE
ALTER TABLE "RoomMembers" ALTER COLUMN "IsMuted" SET DEFAULT FALSE;
