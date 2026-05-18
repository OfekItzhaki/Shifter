namespace Jobuler.Infrastructure.Timezone;

/// <summary>
/// Static mapping of (CountryCode, StateCode?) → IANA timezone identifier.
/// Used by TimezoneResolver to determine a user's timezone from their geographic selection.
/// 
/// Sources:
/// - IANA Time Zone Database (tzdata), maintained by ICANN
/// - ISO 3166-1 alpha-2 country codes
/// - ISO 3166-2 subdivision codes for multi-timezone countries
/// 
/// Design decisions:
/// - Single-timezone countries are included in CountryMappings (state is ignored by resolver)
/// - Multi-timezone countries have state-level entries in StateMappings (key: "CC-STATE")
///   plus a fallback entry in CountryMappings (most populous timezone)
/// - The ultimate fallback (Asia/Jerusalem) is handled by the resolver, not this map
/// </summary>
public static class CountryTimezoneMap
{
    /// <summary>
    /// Country-level timezone mappings.
    /// For single-timezone countries: the only timezone.
    /// For multi-timezone countries: the most populous timezone (used as fallback when no state is provided).
    /// Key: ISO 3166-1 alpha-2 country code (uppercase). Value: IANA timezone ID.
    /// </summary>
    public static IReadOnlyDictionary<string, string> CountryMappings { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // ─── Multi-timezone countries (most populous timezone fallback) ──
            ["US"] = "America/New_York",
            ["RU"] = "Europe/Moscow",
            ["AU"] = "Australia/Sydney",
            ["CA"] = "America/Toronto",
            ["BR"] = "America/Sao_Paulo",
            ["MX"] = "America/Mexico_City",
            ["ID"] = "Asia/Jakarta",
            ["CL"] = "America/Santiago",
            ["KZ"] = "Asia/Almaty",
            ["CN"] = "Asia/Shanghai",
            ["MN"] = "Asia/Ulaanbaatar",
            ["CD"] = "Africa/Kinshasa",
            ["AR"] = "America/Argentina/Buenos_Aires",
            ["PT"] = "Europe/Lisbon",
            ["ES"] = "Europe/Madrid",
            ["NZ"] = "Pacific/Auckland",

            // ─── Single-timezone countries ───────────────────────────────────

            // Middle East
            ["IL"] = "Asia/Jerusalem",
            ["JO"] = "Asia/Amman",
            ["LB"] = "Asia/Beirut",
            ["SY"] = "Asia/Damascus",
            ["IQ"] = "Asia/Baghdad",
            ["KW"] = "Asia/Kuwait",
            ["SA"] = "Asia/Riyadh",
            ["BH"] = "Asia/Bahrain",
            ["QA"] = "Asia/Qatar",
            ["AE"] = "Asia/Dubai",
            ["OM"] = "Asia/Muscat",
            ["YE"] = "Asia/Aden",
            ["IR"] = "Asia/Tehran",

            // Europe
            ["GB"] = "Europe/London",
            ["IE"] = "Europe/Dublin",
            ["IS"] = "Atlantic/Reykjavik",
            ["FR"] = "Europe/Paris",
            ["DE"] = "Europe/Berlin",
            ["IT"] = "Europe/Rome",
            ["NL"] = "Europe/Amsterdam",
            ["BE"] = "Europe/Brussels",
            ["LU"] = "Europe/Luxembourg",
            ["CH"] = "Europe/Zurich",
            ["AT"] = "Europe/Vienna",
            ["PL"] = "Europe/Warsaw",
            ["CZ"] = "Europe/Prague",
            ["SK"] = "Europe/Bratislava",
            ["HU"] = "Europe/Budapest",
            ["RO"] = "Europe/Bucharest",
            ["BG"] = "Europe/Sofia",
            ["GR"] = "Europe/Athens",
            ["TR"] = "Europe/Istanbul",
            ["FI"] = "Europe/Helsinki",
            ["SE"] = "Europe/Stockholm",
            ["NO"] = "Europe/Oslo",
            ["DK"] = "Europe/Copenhagen",
            ["EE"] = "Europe/Tallinn",
            ["LV"] = "Europe/Riga",
            ["LT"] = "Europe/Vilnius",
            ["HR"] = "Europe/Zagreb",
            ["SI"] = "Europe/Ljubljana",
            ["RS"] = "Europe/Belgrade",
            ["BA"] = "Europe/Sarajevo",
            ["ME"] = "Europe/Podgorica",
            ["MK"] = "Europe/Skopje",
            ["AL"] = "Europe/Tirane",
            ["XK"] = "Europe/Belgrade", // Kosovo
            ["UA"] = "Europe/Kyiv",
            ["MD"] = "Europe/Chisinau",
            ["BY"] = "Europe/Minsk",
            ["MT"] = "Europe/Malta",
            ["CY"] = "Asia/Nicosia",
            ["AD"] = "Europe/Andorra",
            ["MC"] = "Europe/Monaco",
            ["SM"] = "Europe/San_Marino",
            ["VA"] = "Europe/Vatican",
            ["LI"] = "Europe/Vaduz",

            // Asia
            ["IN"] = "Asia/Kolkata",
            ["PK"] = "Asia/Karachi",
            ["BD"] = "Asia/Dhaka",
            ["LK"] = "Asia/Colombo",
            ["NP"] = "Asia/Kathmandu",
            ["MM"] = "Asia/Yangon",
            ["TH"] = "Asia/Bangkok",
            ["VN"] = "Asia/Ho_Chi_Minh",
            ["KH"] = "Asia/Phnom_Penh",
            ["LA"] = "Asia/Vientiane",
            ["MY"] = "Asia/Kuala_Lumpur",
            ["SG"] = "Asia/Singapore",
            ["PH"] = "Asia/Manila",
            ["JP"] = "Asia/Tokyo",
            ["KR"] = "Asia/Seoul",
            ["KP"] = "Asia/Pyongyang",
            ["TW"] = "Asia/Taipei",
            ["HK"] = "Asia/Hong_Kong",
            ["MO"] = "Asia/Macau",
            ["AF"] = "Asia/Kabul",
            ["UZ"] = "Asia/Tashkent",
            ["TM"] = "Asia/Ashgabat",
            ["TJ"] = "Asia/Dushanbe",
            ["KG"] = "Asia/Bishkek",
            ["AZ"] = "Asia/Baku",
            ["GE"] = "Asia/Tbilisi",
            ["AM"] = "Asia/Yerevan",
            ["BN"] = "Asia/Brunei",
            ["TL"] = "Asia/Dili",
            ["BT"] = "Asia/Thimphu",
            ["MV"] = "Indian/Maldives",

            // Africa
            ["EG"] = "Africa/Cairo",
            ["ZA"] = "Africa/Johannesburg",
            ["NG"] = "Africa/Lagos",
            ["KE"] = "Africa/Nairobi",
            ["ET"] = "Africa/Addis_Ababa",
            ["GH"] = "Africa/Accra",
            ["TZ"] = "Africa/Dar_es_Salaam",
            ["UG"] = "Africa/Kampala",
            ["MA"] = "Africa/Casablanca",
            ["DZ"] = "Africa/Algiers",
            ["TN"] = "Africa/Tunis",
            ["LY"] = "Africa/Tripoli",
            ["SD"] = "Africa/Khartoum",
            ["SS"] = "Africa/Juba",
            ["SN"] = "Africa/Dakar",
            ["CI"] = "Africa/Abidjan",
            ["CM"] = "Africa/Douala",
            ["AO"] = "Africa/Luanda",
            ["MZ"] = "Africa/Maputo",
            ["ZW"] = "Africa/Harare",
            ["ZM"] = "Africa/Lusaka",
            ["BW"] = "Africa/Gaborone",
            ["NA"] = "Africa/Windhoek",
            ["MW"] = "Africa/Blantyre",
            ["RW"] = "Africa/Kigali",
            ["MG"] = "Indian/Antananarivo",
            ["MU"] = "Indian/Mauritius",

            // Americas
            ["CO"] = "America/Bogota",
            ["VE"] = "America/Caracas",
            ["PE"] = "America/Lima",
            ["EC"] = "America/Guayaquil",
            ["BO"] = "America/La_Paz",
            ["PY"] = "America/Asuncion",
            ["UY"] = "America/Montevideo",
            ["GY"] = "America/Guyana",
            ["SR"] = "America/Paramaribo",
            ["PA"] = "America/Panama",
            ["CR"] = "America/Costa_Rica",
            ["NI"] = "America/Managua",
            ["HN"] = "America/Tegucigalpa",
            ["SV"] = "America/El_Salvador",
            ["GT"] = "America/Guatemala",
            ["BZ"] = "America/Belize",
            ["CU"] = "America/Havana",
            ["JM"] = "America/Jamaica",
            ["HT"] = "America/Port-au-Prince",
            ["DO"] = "America/Santo_Domingo",
            ["TT"] = "America/Port_of_Spain",
            ["BB"] = "America/Barbados",
            ["BS"] = "America/Nassau",

            // Oceania
            ["FJ"] = "Pacific/Fiji",
            ["PG"] = "Pacific/Port_Moresby",
            ["WS"] = "Pacific/Apia",
            ["TO"] = "Pacific/Tongatapu",
        };

    /// <summary>
    /// State-level timezone mappings for multi-timezone countries.
    /// Key format: "CC-STATE" (e.g., "US-NY", "AU-NSW"). Value: IANA timezone ID.
    /// Used when a country spans multiple timezones and the user has specified a state.
    /// </summary>
    public static IReadOnlyDictionary<string, string> StateMappings { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // ─── United States ───────────────────────────────────────────────
            // Eastern
            ["US-NY"] = "America/New_York",
            ["US-FL"] = "America/New_York",
            ["US-GA"] = "America/New_York",
            ["US-NC"] = "America/New_York",
            ["US-VA"] = "America/New_York",
            ["US-MA"] = "America/New_York",
            ["US-PA"] = "America/New_York",
            ["US-NJ"] = "America/New_York",
            ["US-CT"] = "America/New_York",
            ["US-MD"] = "America/New_York",
            ["US-SC"] = "America/New_York",
            ["US-ME"] = "America/New_York",
            ["US-NH"] = "America/New_York",
            ["US-VT"] = "America/New_York",
            ["US-RI"] = "America/New_York",
            ["US-DE"] = "America/New_York",
            ["US-DC"] = "America/New_York",
            ["US-WV"] = "America/New_York",
            ["US-OH"] = "America/New_York",
            ["US-MI"] = "America/New_York",
            // Central
            ["US-IL"] = "America/Chicago",
            ["US-TX"] = "America/Chicago",
            ["US-MN"] = "America/Chicago",
            ["US-WI"] = "America/Chicago",
            ["US-IA"] = "America/Chicago",
            ["US-MO"] = "America/Chicago",
            ["US-AR"] = "America/Chicago",
            ["US-LA"] = "America/Chicago",
            ["US-MS"] = "America/Chicago",
            ["US-AL"] = "America/Chicago",
            ["US-TN"] = "America/Chicago",
            ["US-KY"] = "America/Chicago",
            ["US-KS"] = "America/Chicago",
            ["US-OK"] = "America/Chicago",
            ["US-NE"] = "America/Chicago",
            ["US-SD"] = "America/Chicago",
            ["US-ND"] = "America/Chicago",
            // Indiana
            ["US-IN"] = "America/Indiana/Indianapolis",
            // Mountain
            ["US-CO"] = "America/Denver",
            ["US-AZ"] = "America/Phoenix",
            ["US-UT"] = "America/Denver",
            ["US-MT"] = "America/Denver",
            ["US-WY"] = "America/Denver",
            ["US-NM"] = "America/Denver",
            ["US-ID"] = "America/Boise",
            // Pacific
            ["US-CA"] = "America/Los_Angeles",
            ["US-WA"] = "America/Los_Angeles",
            ["US-OR"] = "America/Los_Angeles",
            ["US-NV"] = "America/Los_Angeles",
            // Alaska & Hawaii
            ["US-AK"] = "America/Anchorage",
            ["US-HI"] = "Pacific/Honolulu",

            // ─── Russia ──────────────────────────────────────────────────────
            ["RU-MOW"] = "Europe/Moscow",
            ["RU-SPE"] = "Europe/Moscow",
            ["RU-KDA"] = "Europe/Moscow",
            ["RU-ROS"] = "Europe/Moscow",
            ["RU-NIZ"] = "Europe/Moscow",
            ["RU-TAT"] = "Europe/Moscow",
            ["RU-SAM"] = "Europe/Samara",
            ["RU-SVE"] = "Asia/Yekaterinburg",
            ["RU-CHE"] = "Asia/Yekaterinburg",
            ["RU-TYU"] = "Asia/Yekaterinburg",
            ["RU-OMS"] = "Asia/Omsk",
            ["RU-NVS"] = "Asia/Novosibirsk",
            ["RU-KYA"] = "Asia/Krasnoyarsk",
            ["RU-IRK"] = "Asia/Irkutsk",
            ["RU-PRI"] = "Asia/Vladivostok",
            ["RU-KHA"] = "Asia/Vladivostok",
            ["RU-SAK"] = "Asia/Sakhalin",
            ["RU-KAM"] = "Asia/Kamchatka",

            // ─── Australia ───────────────────────────────────────────────────
            ["AU-NSW"] = "Australia/Sydney",
            ["AU-VIC"] = "Australia/Melbourne",
            ["AU-QLD"] = "Australia/Brisbane",
            ["AU-WA"] = "Australia/Perth",
            ["AU-SA"] = "Australia/Adelaide",
            ["AU-TAS"] = "Australia/Hobart",
            ["AU-NT"] = "Australia/Darwin",
            ["AU-ACT"] = "Australia/Sydney",

            // ─── Canada ──────────────────────────────────────────────────────
            ["CA-ON"] = "America/Toronto",
            ["CA-QC"] = "America/Toronto",
            ["CA-BC"] = "America/Vancouver",
            ["CA-AB"] = "America/Edmonton",
            ["CA-SK"] = "America/Regina",
            ["CA-MB"] = "America/Winnipeg",
            ["CA-NS"] = "America/Halifax",
            ["CA-NB"] = "America/Halifax",
            ["CA-PE"] = "America/Halifax",
            ["CA-NL"] = "America/St_Johns",
            ["CA-NT"] = "America/Yellowknife",
            ["CA-YT"] = "America/Whitehorse",
            ["CA-NU"] = "America/Iqaluit",

            // ─── Brazil ──────────────────────────────────────────────────────
            ["BR-SP"] = "America/Sao_Paulo",
            ["BR-RJ"] = "America/Sao_Paulo",
            ["BR-MG"] = "America/Sao_Paulo",
            ["BR-PR"] = "America/Sao_Paulo",
            ["BR-RS"] = "America/Sao_Paulo",
            ["BR-SC"] = "America/Sao_Paulo",
            ["BR-BA"] = "America/Bahia",
            ["BR-AM"] = "America/Manaus",
            ["BR-PA"] = "America/Belem",
            ["BR-MT"] = "America/Cuiaba",
            ["BR-MS"] = "America/Campo_Grande",
            ["BR-AC"] = "America/Rio_Branco",
            ["BR-CE"] = "America/Fortaleza",
            ["BR-PE"] = "America/Recife",

            // ─── Mexico ──────────────────────────────────────────────────────
            ["MX-CMX"] = "America/Mexico_City",
            ["MX-JAL"] = "America/Mexico_City",
            ["MX-NLE"] = "America/Monterrey",
            ["MX-BCN"] = "America/Tijuana",
            ["MX-BCS"] = "America/Mazatlan",
            ["MX-SIN"] = "America/Mazatlan",
            ["MX-SON"] = "America/Hermosillo",
            ["MX-CHH"] = "America/Chihuahua",

            // ─── Indonesia ───────────────────────────────────────────────────
            ["ID-JK"] = "Asia/Jakarta",
            ["ID-JB"] = "Asia/Jakarta",
            ["ID-JT"] = "Asia/Jakarta",
            ["ID-JI"] = "Asia/Jakarta",
            ["ID-KT"] = "Asia/Makassar",
            ["ID-KS"] = "Asia/Makassar",
            ["ID-BA"] = "Asia/Makassar",
            ["ID-NT"] = "Asia/Makassar",
            ["ID-PA"] = "Asia/Jayapura",
            ["ID-PB"] = "Asia/Jayapura",

            // ─── Chile ───────────────────────────────────────────────────────
            ["CL-RM"] = "America/Santiago",
            ["CL-VS"] = "America/Santiago",
            ["CL-BI"] = "America/Santiago",
            ["CL-IP"] = "Pacific/Easter",

            // ─── Kazakhstan ──────────────────────────────────────────────────
            ["KZ-ALA"] = "Asia/Almaty",
            ["KZ-AST"] = "Asia/Almaty",
            ["KZ-AKT"] = "Asia/Aqtau",
            ["KZ-MAN"] = "Asia/Aqtau",
            ["KZ-ATY"] = "Asia/Aqtobe",

            // ─── China ───────────────────────────────────────────────────────
            // China officially uses a single timezone (Asia/Shanghai) but we map
            // the far-west Xinjiang region to Urumqi for practical accuracy.
            ["CN-XJ"] = "Asia/Urumqi",

            // ─── Mongolia ────────────────────────────────────────────────────
            ["MN-UB"] = "Asia/Ulaanbaatar",
            ["MN-HOV"] = "Asia/Hovd",

            // ─── DR Congo ────────────────────────────────────────────────────
            ["CD-KN"] = "Africa/Kinshasa",
            ["CD-LB"] = "Africa/Lubumbashi",

            // ─── Argentina ───────────────────────────────────────────────────
            ["AR-BA"] = "America/Argentina/Buenos_Aires",
            ["AR-CF"] = "America/Argentina/Buenos_Aires",
            ["AR-SL"] = "America/Argentina/San_Luis",

            // ─── Portugal ────────────────────────────────────────────────────
            ["PT-30"] = "Atlantic/Azores",
            ["PT-20"] = "Atlantic/Madeira",

            // ─── Spain ───────────────────────────────────────────────────────
            ["ES-CN"] = "Atlantic/Canary",

            // ─── New Zealand ─────────────────────────────────────────────────
            ["NZ-CIT"] = "Pacific/Chatham",
        };

    /// <summary>
    /// Set of country codes that span multiple timezones.
    /// Used to determine whether the State/Region dropdown should be shown in the UI.
    /// </summary>
    public static IReadOnlySet<string> MultiTimezoneCountries { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "US", "RU", "AU", "CA", "BR", "MX", "ID", "CL",
            "KZ", "CN", "MN", "CD", "AR", "PT", "ES", "NZ"
        };

    /// <summary>
    /// Returns true if the given country code is a multi-timezone country
    /// (i.e., has state-level mappings and the UI should show a state selector).
    /// </summary>
    public static bool IsMultiTimezoneCountry(string? countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return false;

        return MultiTimezoneCountries.Contains(countryCode.Trim().ToUpperInvariant());
    }
}
