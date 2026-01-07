-- Supprimer les anciennes tables si elles existent
DROP TABLE IF EXISTS "RoomRoleRequests" CASCADE;
DROP TABLE IF EXISTS "RoomMemberRoles" CASCADE;

-- Créer la nouvelle table simplifiée
CREATE TABLE IF NOT EXISTS "RoomAdmins" (
    "Id" SERIAL PRIMARY KEY,
    "RoomId" INT NOT NULL REFERENCES "Rooms"("Id") ON DELETE CASCADE,
    "UserId" INT NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Role" VARCHAR(20) NOT NULL CHECK ("Role" IN ('SuperAdmin', 'Admin', 'Moderator')),
    "AssignedBy" INT NOT NULL REFERENCES "Users"("Id"),
    "AssignedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    UNIQUE("RoomId", "UserId")
);

-- Index pour les requêtes fréquentes
CREATE INDEX IF NOT EXISTS idx_roomadmins_room ON "RoomAdmins"("RoomId");
CREATE INDEX IF NOT EXISTS idx_roomadmins_user ON "RoomAdmins"("UserId");

-- Verification
SELECT 'Table RoomAdmins créée avec succès' AS status;
