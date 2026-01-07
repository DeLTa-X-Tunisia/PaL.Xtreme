-- Table des attributions de rôles dans les salons (User <-> Room)
CREATE TABLE IF NOT EXISTS "RoomMemberRoles" (
    "Id" SERIAL PRIMARY KEY,
    "RoomId" INTEGER NOT NULL REFERENCES "Rooms"("Id") ON DELETE CASCADE,
    "UserId" INTEGER NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Role" VARCHAR(50) NOT NULL,
    "AssignedBy" INTEGER NOT NULL REFERENCES "Users"("Id"),
    "AssignedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "RemovedAt" TIMESTAMP NULL,
    CONSTRAINT "UQ_RoomMemberRoles_Room_User" UNIQUE ("RoomId", "UserId")
);

CREATE INDEX IF NOT EXISTS "IX_RoomMemberRoles_RoomId" ON "RoomMemberRoles"("RoomId");
CREATE INDEX IF NOT EXISTS "IX_RoomMemberRoles_UserId" ON "RoomMemberRoles"("UserId");
CREATE INDEX IF NOT EXISTS "IX_RoomMemberRoles_Active" ON "RoomMemberRoles"("RoomId", "IsActive");
