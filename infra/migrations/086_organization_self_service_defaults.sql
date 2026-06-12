CREATE TABLE IF NOT EXISTS organization_self_service_defaults (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  organization_id uuid NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
  min_shifts_per_cycle integer NOT NULL DEFAULT 0,
  max_shifts_per_cycle integer NOT NULL DEFAULT 7,
  request_window_open_offset_hours integer NOT NULL DEFAULT 168,
  request_window_close_offset_hours integer NOT NULL DEFAULT 24,
  cancellation_cutoff_hours integer NOT NULL DEFAULT 24,
  max_absences_per_cycle integer NOT NULL DEFAULT 3,
  max_late_cancellations_per_cycle integer NOT NULL DEFAULT 2,
  late_cancellation_window_hours integer NOT NULL DEFAULT 24,
  waitlist_offer_minutes integer NOT NULL DEFAULT 60,
  cycle_duration_days integer NOT NULL DEFAULT 7,
  allow_member_shift_claims boolean NOT NULL DEFAULT true,
  allow_waitlist boolean NOT NULL DEFAULT true,
  allow_shift_change_requests boolean NOT NULL DEFAULT true,
  allow_absence_reports boolean NOT NULL DEFAULT true,
  allow_shift_swaps boolean NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS IX_organization_self_service_defaults_organization_id
  ON organization_self_service_defaults(organization_id);
