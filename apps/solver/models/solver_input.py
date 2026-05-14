"""
Solver input contract — matches the payload the API sends to the solver.
All fields mirror the spec's Section 17.1.
"""
from pydantic import BaseModel
from datetime import datetime, date
from typing import Optional
from enum import Enum


class TriggerMode(str, Enum):
    standard = "standard"
    emergency = "emergency"


class StabilityWeights(BaseModel):
    today_tomorrow: float = 10.0   # very high penalty for near-term changes
    days_3_4: float = 3.0          # medium penalty
    days_5_7: float = 1.0          # low penalty


class PersonEligibility(BaseModel):
    person_id: str
    role_ids: list[str]
    qualification_ids: list[str]
    group_ids: list[str]
    home_leave_priority: float = 1.0  # multiplier for home-leave fairness (1.0=normal, 2.0=more home time)


class AvailabilityWindow(BaseModel):
    person_id: str
    starts_at: datetime
    ends_at: datetime


class PresenceWindow(BaseModel):
    person_id: str
    state: str  # free_in_base | at_home | on_mission
    starts_at: datetime
    ends_at: datetime


class QualificationRequirement(BaseModel):
    qualification_name: str
    count: int
    mandatory: bool


class TaskSlot(BaseModel):
    slot_id: str
    task_type_id: str
    task_type_name: str
    burden_level: str  # favorable | neutral | disliked | hated
    starts_at: datetime
    ends_at: datetime
    required_headcount: int
    priority: int
    required_role_ids: list[str]
    required_qualification_ids: list[str]
    allows_overlap: bool
    allows_double_shift: bool = False
    qualification_requirements: list[QualificationRequirement] = []


class HardConstraint(BaseModel):
    constraint_id: str
    rule_type: str
    scope_type: str
    scope_id: Optional[str]
    payload: dict


class SoftConstraint(BaseModel):
    constraint_id: str
    rule_type: str
    scope_type: str
    scope_id: Optional[str]
    weight: float
    payload: dict


class BaselineAssignment(BaseModel):
    slot_id: str
    person_id: str


class FairnessCounters(BaseModel):
    person_id: str
    total_assignments_7d: int = 0
    hated_tasks_7d: int = 0
    disliked_hated_score_7d: int = 0
    kitchen_count_7d: int = 0
    night_missions_7d: int = 0
    consecutive_burden_count: int = 0


class HomeLeaveConfig(BaseModel):
    enabled: bool
    min_rest_hours: float
    eligibility_threshold_hours: float
    leave_capacity: int
    leave_duration_hours: float
    balance_value: int = 50  # 0–100, maps to weight 0–400


class TaskRotation(BaseModel):
    person_id: str
    completed_task_type_ids: list[str]


class SolverInput(BaseModel):
    space_id: str
    run_id: str
    trigger_mode: TriggerMode
    horizon_start: date
    horizon_end: date
    locale: str = "en"  # he | en | ru — controls language of conflict descriptions
    stability_weights: StabilityWeights
    people: list[PersonEligibility]
    availability_windows: list[AvailabilityWindow]
    presence_windows: list[PresenceWindow]
    task_slots: list[TaskSlot]
    hard_constraints: list[HardConstraint]
    soft_constraints: list[SoftConstraint]
    emergency_constraints: list[HardConstraint] = []  # bypass all hard/soft constraints
    baseline_assignments: list[BaselineAssignment]
    fairness_counters: list[FairnessCounters]
    locked_slot_ids: Optional[list[str]] = []  # slot IDs with manual overrides — solver must not reassign these
    home_leave_config: Optional[HomeLeaveConfig] = None
    preview_mode: bool = False  # when True, solver uses reduced time limit and single worker
    task_rotation: Optional[list[TaskRotation]] = None  # rotation data for army-template groups

    @property
    def locked_slot_ids_safe(self) -> list[str]:
        return self.locked_slot_ids or []
