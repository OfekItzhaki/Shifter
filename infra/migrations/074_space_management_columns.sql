-- Migration 074: Add space management columns (deleted_by_space_deletion, space deleted_at, management_timeout_minutes on spaces)
--
-- Supports soft-delete cascade from spaces to groups, and space-level management timeout.

-- ─── 1. Add deleted_by_space_deletion to groups ───────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'groups' AND column_name = 'deleted_by_space_deletion'
    ) THEN
        ALTER TABLE groups
            ADD COLUMN deleted_by_space_deletion BOOLEAN NOT NULL DEFAULT FALSE;

        RAISE NOTICE 'Added deleted_by_space_deletion column to groups';
    ELSE
        RAISE NOTICE 'Column deleted_by_space_deletion already exists on groups — skipping';
    END IF;
END $$;

-- ─── 2. Add deleted_at to spaces ──────────────────────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'spaces' AND column_name = 'deleted_at'
    ) THEN
        ALTER TABLE spaces
            ADD COLUMN deleted_at TIMESTAMPTZ;

        RAISE NOTICE 'Added deleted_at column to spaces';
    ELSE
        RAISE NOTICE 'Column deleted_at already exists on spaces — skipping';
    END IF;
END $$;

-- ─── 3. Add management_timeout_minutes to spaces ──────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'spaces' AND column_name = 'management_timeout_minutes'
    ) THEN
        ALTER TABLE spaces
            ADD COLUMN management_timeout_minutes INTEGER NOT NULL DEFAULT 15;

        RAISE NOTICE 'Added management_timeout_minutes column to spaces';
    ELSE
        RAISE NOTICE 'Column management_timeout_minutes already exists on spaces — skipping';
    END IF;
END $$;

-- ─── Index for space soft-delete queries ──────────────────────────────────────
CREATE INDEX IF NOT EXISTS idx_spaces_deleted_at
    ON spaces (deleted_at)
    WHERE deleted_at IS NOT NULL;

-- ─── 4. Add permission_level to space_memberships ─────────────────────────────
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'space_memberships' AND column_name = 'permission_level'
    ) THEN
        ALTER TABLE space_memberships
            ADD COLUMN permission_level INTEGER NOT NULL DEFAULT 0;

        RAISE NOTICE 'Added permission_level column to space_memberships';
    ELSE
        RAISE NOTICE 'Column permission_level already exists on space_memberships — skipping';
    END IF;
END $$;
