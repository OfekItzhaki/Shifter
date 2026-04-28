"""
Solver i18n — conflict descriptions and explanation fragments.
Default locale is "en". Supported: en, he, ru.
"""

_STRINGS: dict[str, dict[str, str]] = {
    "en": {
        # Explanation fragments
        "solver_timeout":       "Solver reached time limit — returning best known result.",
        "no_feasible":          "No feasible solution found under current hard constraints.",
        "uncovered_slots":      "{n} slot(s) could not be fully staffed.",
        "all_staffed":          "All slots fully staffed.",
        # Conflict descriptions
        "headcount_global":     "Not enough members: {required} total assignments needed but only {available} members.",
        "slot_eligibility":     "Shift '{task}' ({starts_at}) requires {required} people but only {eligible} are eligible ({reasons}).",
        # Reason labels
        "reason_qualification": "qualification requirements",
        "reason_role":          "role requirements",
        "reason_availability":  "availability / presence windows",
        "reason_other":         "various constraints",
        # Suggestion
        "suggestion":           "Resolve by: adding more members, extending the planning horizon, or relaxing constraints.",
    },
    "he": {
        "solver_timeout":       "הסולבר הגיע לגבול הזמן — מוחזרת התוצאה הטובה ביותר שנמצאה.",
        "no_feasible":          "לא נמצא סידור אפשרי עם האילוצים הנוכחיים.",
        "uncovered_slots":      "{n} משמרות לא אויישו במלואן.",
        "all_staffed":          "כל המשמרות אויישו.",
        "headcount_global":     "אין מספיק חברים: נדרשים {required} שיבוצים סה\"כ אך יש רק {available} חברים.",
        "slot_eligibility":     "משמרת '{task}' ({starts_at}) דורשת {required} אנשים אך רק {eligible} כשירים ({reasons}).",
        "reason_qualification": "דרישות כישורים",
        "reason_role":          "דרישות תפקיד",
        "reason_availability":  "חלונות זמינות / נוכחות",
        "reason_other":         "אילוצים שונים",
        "suggestion":           "ניתן לפתור על ידי: הוספת חברים נוספים, הרחבת אופק הזמן, או הקלת אילוצים.",
    },
    "ru": {
        "solver_timeout":       "Решатель достиг лимита времени — возвращается наилучший найденный результат.",
        "no_feasible":          "Не найдено допустимого расписания при текущих ограничениях.",
        "uncovered_slots":      "{n} смен(а) не укомплектованы полностью.",
        "all_staffed":          "Все смены укомплектованы.",
        "headcount_global":     "Недостаточно участников: требуется {required} назначений, но доступно только {available} человек.",
        "slot_eligibility":     "Смена '{task}' ({starts_at}) требует {required} человек, но только {eligible} подходят ({reasons}).",
        "reason_qualification": "требования к квалификации",
        "reason_role":          "требования к роли",
        "reason_availability":  "окна доступности / присутствия",
        "reason_other":         "различные ограничения",
        "suggestion":           "Решение: добавьте больше участников, расширьте горизонт планирования или смягчите ограничения.",
    },
}

# Fallback to English for any unknown locale
_FALLBACK = "en"


def t(locale: str, key: str, **kwargs: object) -> str:
    """Translate a key for the given locale, with optional format args."""
    strings = _STRINGS.get(locale, _STRINGS[_FALLBACK])
    template = strings.get(key, _STRINGS[_FALLBACK].get(key, key))
    return template.format(**kwargs) if kwargs else template
