type PhoneNormalizationResult = {
  value: string;
  isValid: boolean;
};

const COUNTRY_DIALING_CODES: Record<string, string> = {
  AU: "61",
  BR: "55",
  CA: "1",
  DE: "49",
  ES: "34",
  FR: "33",
  GB: "44",
  IL: "972",
  IT: "39",
  NL: "31",
  RU: "7",
  US: "1",
};

const PHONE_PLACEHOLDERS: Record<string, string> = {
  AU: "+61 400 000 000",
  BR: "+55 11 90000 0000",
  CA: "+1 555 000 0000",
  DE: "+49 151 00000000",
  ES: "+34 600 000 000",
  FR: "+33 6 00 00 00 00",
  GB: "+44 7700 900000",
  IL: "+972 50 000 0000",
  IT: "+39 320 000 0000",
  NL: "+31 6 00000000",
  RU: "+7 999 000 00 00",
  US: "+1 555 000 0000",
};

function compactPhoneInput(value: string): string {
  return value
    .trim()
    .replace(/[\s().-]/g, "")
    .replace(/^00/, "+");
}

function isValidE164(value: string): boolean {
  return /^\+[1-9]\d{7,14}$/.test(value);
}

function isValidIsraeliE164(value: string): boolean {
  return /^\+972(?:[23489]\d{7}|[57]\d{8})$/.test(value);
}

function normalizeIsraeliPhone(compact: string): PhoneNormalizationResult {
  const digits = compact.replace(/^\+/, "");
  let value = compact;

  if (digits.startsWith("972")) {
    value = `+${digits}`;
  } else if (digits.startsWith("0")) {
    value = `+972${digits.slice(1)}`;
  } else if (/^[57]\d{8}$/.test(digits) || /^[23489]\d{7}$/.test(digits)) {
    value = `+972${digits}`;
  }

  return { value, isValid: isValidIsraeliE164(value) };
}

export function normalizePhoneNumberForCountry(rawValue: string, countryCode: string): PhoneNormalizationResult {
  const compact = compactPhoneInput(rawValue);
  if (!compact) return { value: "", isValid: true };
  if (!/^\+?\d+$/.test(compact)) return { value: compact, isValid: false };

  const country = countryCode.trim().toUpperCase();
  if (country === "IL") return normalizeIsraeliPhone(compact);

  if (compact.startsWith("+")) {
    return { value: compact, isValid: isValidE164(compact) };
  }

  const dialingCode = COUNTRY_DIALING_CODES[country];
  if (!dialingCode) return { value: compact, isValid: /^\d{7,15}$/.test(compact) };

  const digits = compact.startsWith(dialingCode)
    ? compact
    : `${dialingCode}${compact.startsWith("0") ? compact.slice(1) : compact}`;
  const value = `+${digits}`;
  return { value, isValid: isValidE164(value) };
}

export function normalizePhoneForLooseComparison(rawValue: string | null | undefined): string {
  if (!rawValue) return "";

  const israeli = normalizePhoneNumberForCountry(rawValue, "IL");
  if (israeli.isValid) return israeli.value;

  return compactPhoneInput(rawValue);
}

export function getPhonePlaceholder(countryCode: string): string {
  const country = countryCode.trim().toUpperCase();
  const dialingCode = COUNTRY_DIALING_CODES[country];
  return PHONE_PLACEHOLDERS[country] ?? (dialingCode ? `+${dialingCode}` : "+");
}
