-- Special leave requests: member asks to be away; admin approval creates an AtHome presence window.

CREATE TABLE IF NOT EXISTS special_leave_requests (
  id uuid PRIMARY KEY,
  space_id uuid NOT NULL REFERENCES spaces(id) ON DELETE CASCADE,
  person_id uuid NOT NULL REFERENCES people(id) ON DELETE CASCADE,
  starts_at timestamptz NOT NULL,
  ends_at timestamptz NOT NULL,
  reason varchar(500) NOT NULL,
  status varchar(32) NOT NULL DEFAULT 'Pending',
  requested_by_user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
  processed_by_user_id uuid NULL REFERENCES users(id) ON DELETE SET NULL,
  processed_at timestamptz NULL,
  admin_note varchar(500) NULL,
  presence_window_id uuid NULL REFERENCES presence_windows(id) ON DELETE SET NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_special_leave_requests_time CHECK (ends_at > starts_at),
  CONSTRAINT ck_special_leave_requests_status CHECK (status IN ('Pending', 'Approved', 'Rejected', 'Cancelled'))
);

CREATE INDEX IF NOT EXISTS idx_special_leave_requests_space_status_start
  ON special_leave_requests(space_id, status, starts_at);

CREATE INDEX IF NOT EXISTS idx_special_leave_requests_person_status_start
  ON special_leave_requests(person_id, status, starts_at);

CREATE INDEX IF NOT EXISTS idx_special_leave_requests_presence_window
  ON special_leave_requests(presence_window_id)
  WHERE presence_window_id IS NOT NULL;

INSERT INTO schema_migrations (version, applied_at)
VALUES ('083_special_leave_requests', now())
ON CONFLICT (version) DO NOTHING;
