type DateInputOrder = "DMY" | "MDY" | "YMD";

type DateInputPattern = {
  order: DateInputOrder;
  separator: "/" | "." | "-";
  placeholder: string;
};

type ParseDateResult = {
  isoDate: string;
  isValid: boolean;
};

const MONTH_FIRST_COUNTRIES = new Set(["US"]);
const YEAR_FIRST_COUNTRIES = new Set(["CN", "JP", "KR", "TW", "HU"]);
const DOT_SEPARATOR_COUNTRIES = new Set(["RU"]);

function pad2(value: number): string {
  return String(value).padStart(2, "0");
}

function isRealDate(year: number, month: number, day: number): boolean {
  if (year < 1900 || year > 2100 || month < 1 || month > 12 || day < 1 || day > 31) {
    return false;
  }

  const date = new Date(Date.UTC(year, month - 1, day));
  return date.getUTCFullYear() === year
    && date.getUTCMonth() === month - 1
    && date.getUTCDate() === day;
}

export function getDateInputPattern(countryCode: string, _stateCode?: string | null, _locale?: string): DateInputPattern {
  const country = countryCode.trim().toUpperCase();
  const order: DateInputOrder = MONTH_FIRST_COUNTRIES.has(country)
    ? "MDY"
    : YEAR_FIRST_COUNTRIES.has(country)
      ? "YMD"
      : "DMY";
  const separator = DOT_SEPARATOR_COUNTRIES.has(country) ? "." : order === "YMD" ? "-" : "/";

  return {
    order,
    separator,
    placeholder: order === "MDY"
      ? `mm${separator}dd${separator}yyyy`
      : order === "YMD"
        ? `yyyy${separator}mm${separator}dd`
        : `dd${separator}mm${separator}yyyy`,
  };
}

export function parseLocalizedDateInput(
  value: string,
  countryCode: string,
  stateCode?: string | null,
  locale?: string
): ParseDateResult {
  const trimmed = value.trim();
  if (!trimmed) return { isoDate: "", isValid: true };

  const isoMatch = /^(\d{4})-(\d{2})-(\d{2})$/.exec(trimmed);
  if (isoMatch) {
    const year = Number(isoMatch[1]);
    const month = Number(isoMatch[2]);
    const day = Number(isoMatch[3]);
    return {
      isoDate: `${year}-${pad2(month)}-${pad2(day)}`,
      isValid: isRealDate(year, month, day),
    };
  }

  const parts = trimmed.split(/[./-]/).filter(Boolean);
  if (parts.length !== 3 || parts.some(part => !/^\d{1,4}$/.test(part))) {
    return { isoDate: "", isValid: false };
  }

  const pattern = getDateInputPattern(countryCode, stateCode, locale);
  let year: number;
  let month: number;
  let day: number;

  if (pattern.order === "MDY") {
    month = Number(parts[0]);
    day = Number(parts[1]);
    year = Number(parts[2]);
  } else if (pattern.order === "YMD") {
    year = Number(parts[0]);
    month = Number(parts[1]);
    day = Number(parts[2]);
  } else {
    day = Number(parts[0]);
    month = Number(parts[1]);
    year = Number(parts[2]);
  }

  return {
    isoDate: `${year}-${pad2(month)}-${pad2(day)}`,
    isValid: isRealDate(year, month, day),
  };
}

export function formatIsoDateForDateInput(
  isoDate: string,
  countryCode: string,
  stateCode?: string | null,
  locale?: string
): string {
  const parsed = parseLocalizedDateInput(isoDate, countryCode, stateCode, locale);
  if (!parsed.isValid || !parsed.isoDate) return isoDate;

  const [year, month, day] = parsed.isoDate.split("-");
  const pattern = getDateInputPattern(countryCode, stateCode, locale);
  if (pattern.order === "MDY") return `${month}${pattern.separator}${day}${pattern.separator}${year}`;
  if (pattern.order === "YMD") return `${year}${pattern.separator}${month}${pattern.separator}${day}`;
  return `${day}${pattern.separator}${month}${pattern.separator}${year}`;
}
