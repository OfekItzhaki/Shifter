/**
 * Country and state data for timezone location selection.
 * Mirrors the backend CountryTimezoneMap — multi-timezone countries
 * have state-level entries that determine the timezone.
 *
 * Country names are provided in English, Hebrew, and Russian for i18n.
 */

export interface CountryEntry {
  code: string;
  name: { en: string; he: string; ru: string };
}

export interface StateEntry {
  code: string; // e.g. "NY", "NSW" (without country prefix)
  name: { en: string; he: string; ru: string };
}

/** ISO 3166-1 alpha-2 country codes that span multiple timezones */
export const MULTI_TIMEZONE_COUNTRIES = new Set([
  "US", "RU", "AU", "CA", "BR", "MX", "ID", "CL", "KZ", "CN", "MN", "CD", "AR", "PT", "ES", "NZ",
]);

/** All supported countries */
export const COUNTRIES: CountryEntry[] = [
  // Multi-timezone
  { code: "US", name: { en: "United States", he: "ארצות הברית", ru: "США" } },
  { code: "RU", name: { en: "Russia", he: "רוסיה", ru: "Россия" } },
  { code: "AU", name: { en: "Australia", he: "אוסטרליה", ru: "Австралия" } },
  { code: "CA", name: { en: "Canada", he: "קנדה", ru: "Канада" } },
  { code: "BR", name: { en: "Brazil", he: "ברזיל", ru: "Бразилия" } },
  { code: "MX", name: { en: "Mexico", he: "מקסיקו", ru: "Мексика" } },
  { code: "ID", name: { en: "Indonesia", he: "אינדונזיה", ru: "Индонезия" } },
  { code: "CL", name: { en: "Chile", he: "צ'ילה", ru: "Чили" } },
  { code: "KZ", name: { en: "Kazakhstan", he: "קזחסטן", ru: "Казахстан" } },
  { code: "CN", name: { en: "China", he: "סין", ru: "Китай" } },
  { code: "MN", name: { en: "Mongolia", he: "מונגוליה", ru: "Монголия" } },
  { code: "CD", name: { en: "DR Congo", he: "קונגו", ru: "ДР Конго" } },
  { code: "AR", name: { en: "Argentina", he: "ארגנטינה", ru: "Аргентина" } },
  { code: "PT", name: { en: "Portugal", he: "פורטוגל", ru: "Португалия" } },
  { code: "ES", name: { en: "Spain", he: "ספרד", ru: "Испания" } },
  { code: "NZ", name: { en: "New Zealand", he: "ניו זילנד", ru: "Новая Зеландия" } },
  // Middle East
  { code: "IL", name: { en: "Israel", he: "ישראל", ru: "Израиль" } },
  { code: "JO", name: { en: "Jordan", he: "ירדן", ru: "Иордания" } },
  { code: "LB", name: { en: "Lebanon", he: "לבנון", ru: "Ливан" } },
  { code: "SY", name: { en: "Syria", he: "סוריה", ru: "Сирия" } },
  { code: "IQ", name: { en: "Iraq", he: "עיראק", ru: "Ирак" } },
  { code: "KW", name: { en: "Kuwait", he: "כווית", ru: "Кувейт" } },
  { code: "SA", name: { en: "Saudi Arabia", he: "ערב הסעודית", ru: "Саудовская Аравия" } },
  { code: "BH", name: { en: "Bahrain", he: "בחריין", ru: "Бахрейн" } },
  { code: "QA", name: { en: "Qatar", he: "קטאר", ru: "Катар" } },
  { code: "AE", name: { en: "UAE", he: "איחוד האמירויות", ru: "ОАЭ" } },
  { code: "OM", name: { en: "Oman", he: "עומאן", ru: "Оман" } },
  { code: "YE", name: { en: "Yemen", he: "תימן", ru: "Йемен" } },
  { code: "IR", name: { en: "Iran", he: "איראן", ru: "Иран" } },
  // Europe
  { code: "GB", name: { en: "United Kingdom", he: "בריטניה", ru: "Великобритания" } },
  { code: "IE", name: { en: "Ireland", he: "אירלנד", ru: "Ирландия" } },
  { code: "IS", name: { en: "Iceland", he: "איסלנד", ru: "Исландия" } },
  { code: "FR", name: { en: "France", he: "צרפת", ru: "Франция" } },
  { code: "DE", name: { en: "Germany", he: "גרמניה", ru: "Германия" } },
  { code: "IT", name: { en: "Italy", he: "איטליה", ru: "Италия" } },
  { code: "NL", name: { en: "Netherlands", he: "הולנד", ru: "Нидерланды" } },
  { code: "BE", name: { en: "Belgium", he: "בלגיה", ru: "Бельгия" } },
  { code: "LU", name: { en: "Luxembourg", he: "לוקסמבורג", ru: "Люксембург" } },
  { code: "CH", name: { en: "Switzerland", he: "שוויץ", ru: "Швейцария" } },
  { code: "AT", name: { en: "Austria", he: "אוסטריה", ru: "Австрия" } },
  { code: "PL", name: { en: "Poland", he: "פולין", ru: "Польша" } },
  { code: "CZ", name: { en: "Czech Republic", he: "צ'כיה", ru: "Чехия" } },
  { code: "SK", name: { en: "Slovakia", he: "סלובקיה", ru: "Словакия" } },
  { code: "HU", name: { en: "Hungary", he: "הונגריה", ru: "Венгрия" } },
  { code: "RO", name: { en: "Romania", he: "רומניה", ru: "Румыния" } },
  { code: "BG", name: { en: "Bulgaria", he: "בולגריה", ru: "Болгария" } },
  { code: "GR", name: { en: "Greece", he: "יוון", ru: "Греция" } },
  { code: "TR", name: { en: "Turkey", he: "טורקיה", ru: "Турция" } },
  { code: "FI", name: { en: "Finland", he: "פינלנד", ru: "Финляндия" } },
  { code: "SE", name: { en: "Sweden", he: "שוודיה", ru: "Швеция" } },
  { code: "NO", name: { en: "Norway", he: "נורווגיה", ru: "Норвегия" } },
  { code: "DK", name: { en: "Denmark", he: "דנמרק", ru: "Дания" } },
  { code: "EE", name: { en: "Estonia", he: "אסטוניה", ru: "Эстония" } },
  { code: "LV", name: { en: "Latvia", he: "לטביה", ru: "Латвия" } },
  { code: "LT", name: { en: "Lithuania", he: "ליטא", ru: "Литва" } },
  { code: "HR", name: { en: "Croatia", he: "קרואטיה", ru: "Хорватия" } },
  { code: "SI", name: { en: "Slovenia", he: "סלובניה", ru: "Словения" } },
  { code: "RS", name: { en: "Serbia", he: "סרביה", ru: "Сербия" } },
  { code: "UA", name: { en: "Ukraine", he: "אוקראינה", ru: "Украина" } },
  { code: "BY", name: { en: "Belarus", he: "בלארוס", ru: "Беларусь" } },
  { code: "MD", name: { en: "Moldova", he: "מולדובה", ru: "Молдова" } },
  // Asia
  { code: "IN", name: { en: "India", he: "הודו", ru: "Индия" } },
  { code: "PK", name: { en: "Pakistan", he: "פקיסטן", ru: "Пакистан" } },
  { code: "BD", name: { en: "Bangladesh", he: "בנגלדש", ru: "Бангладеш" } },
  { code: "LK", name: { en: "Sri Lanka", he: "סרי לנקה", ru: "Шри-Ланка" } },
  { code: "NP", name: { en: "Nepal", he: "נפאל", ru: "Непал" } },
  { code: "TH", name: { en: "Thailand", he: "תאילנד", ru: "Таиланд" } },
  { code: "VN", name: { en: "Vietnam", he: "וייטנאם", ru: "Вьетнам" } },
  { code: "MY", name: { en: "Malaysia", he: "מלזיה", ru: "Малайзия" } },
  { code: "SG", name: { en: "Singapore", he: "סינגפור", ru: "Сингапур" } },
  { code: "PH", name: { en: "Philippines", he: "הפיליפינים", ru: "Филиппины" } },
  { code: "JP", name: { en: "Japan", he: "יפן", ru: "Япония" } },
  { code: "KR", name: { en: "South Korea", he: "דרום קוריאה", ru: "Южная Корея" } },
  { code: "TW", name: { en: "Taiwan", he: "טייוואן", ru: "Тайвань" } },
  { code: "HK", name: { en: "Hong Kong", he: "הונג קונג", ru: "Гонконг" } },
  { code: "GE", name: { en: "Georgia", he: "גאורגיה", ru: "Грузия" } },
  { code: "AM", name: { en: "Armenia", he: "ארמניה", ru: "Армения" } },
  { code: "AZ", name: { en: "Azerbaijan", he: "אזרבייג'ן", ru: "Азербайджан" } },
  { code: "UZ", name: { en: "Uzbekistan", he: "אוזבקיסטן", ru: "Узбекистан" } },
  // Africa
  { code: "EG", name: { en: "Egypt", he: "מצרים", ru: "Египет" } },
  { code: "ZA", name: { en: "South Africa", he: "דרום אפריקה", ru: "ЮАР" } },
  { code: "NG", name: { en: "Nigeria", he: "ניגריה", ru: "Нигерия" } },
  { code: "KE", name: { en: "Kenya", he: "קניה", ru: "Кения" } },
  { code: "ET", name: { en: "Ethiopia", he: "אתיופיה", ru: "Эфиопия" } },
  { code: "MA", name: { en: "Morocco", he: "מרוקו", ru: "Марокко" } },
  // Americas (single-tz)
  { code: "CO", name: { en: "Colombia", he: "קולומביה", ru: "Колумбия" } },
  { code: "PE", name: { en: "Peru", he: "פרו", ru: "Перу" } },
  { code: "VE", name: { en: "Venezuela", he: "ונצואלה", ru: "Венесуэла" } },
  { code: "EC", name: { en: "Ecuador", he: "אקוודור", ru: "Эквадор" } },
  { code: "UY", name: { en: "Uruguay", he: "אורוגוואי", ru: "Уругвай" } },
  { code: "PA", name: { en: "Panama", he: "פנמה", ru: "Панама" } },
  { code: "CR", name: { en: "Costa Rica", he: "קוסטה ריקה", ru: "Коста-Рика" } },
  { code: "CU", name: { en: "Cuba", he: "קובה", ru: "Куба" } },
  { code: "DO", name: { en: "Dominican Republic", he: "הרפובליקה הדומיניקנית", ru: "Доминиканская Республика" } },
  { code: "JM", name: { en: "Jamaica", he: "ג'מייקה", ru: "Ямайка" } },
  // Oceania
  { code: "FJ", name: { en: "Fiji", he: "פיג'י", ru: "Фиджи" } },
  { code: "PG", name: { en: "Papua New Guinea", he: "פפואה גינאה החדשה", ru: "Папуа-Новая Гвинея" } },
];

/** State/region entries for multi-timezone countries */
export const STATES: Record<string, StateEntry[]> = {
  US: [
    { code: "NY", name: { en: "New York", he: "ניו יורק", ru: "Нью-Йорк" } },
    { code: "CA", name: { en: "California", he: "קליפורניה", ru: "Калифорния" } },
    { code: "TX", name: { en: "Texas", he: "טקסס", ru: "Техас" } },
    { code: "FL", name: { en: "Florida", he: "פלורידה", ru: "Флорида" } },
    { code: "IL", name: { en: "Illinois", he: "אילינוי", ru: "Иллинойс" } },
    { code: "PA", name: { en: "Pennsylvania", he: "פנסילבניה", ru: "Пенсильвания" } },
    { code: "OH", name: { en: "Ohio", he: "אוהיו", ru: "Огайо" } },
    { code: "GA", name: { en: "Georgia", he: "ג'ורג'יה", ru: "Джорджия" } },
    { code: "NC", name: { en: "North Carolina", he: "צפון קרוליינה", ru: "Северная Каролина" } },
    { code: "MI", name: { en: "Michigan", he: "מישיגן", ru: "Мичиган" } },
    { code: "NJ", name: { en: "New Jersey", he: "ניו ג'רזי", ru: "Нью-Джерси" } },
    { code: "VA", name: { en: "Virginia", he: "וירג'יניה", ru: "Виргиния" } },
    { code: "WA", name: { en: "Washington", he: "וושינגטון", ru: "Вашингтон" } },
    { code: "MA", name: { en: "Massachusetts", he: "מסצ'וסטס", ru: "Массачусетс" } },
    { code: "AZ", name: { en: "Arizona", he: "אריזונה", ru: "Аризона" } },
    { code: "CO", name: { en: "Colorado", he: "קולורדו", ru: "Колорадо" } },
    { code: "MN", name: { en: "Minnesota", he: "מינסוטה", ru: "Миннесота" } },
    { code: "WI", name: { en: "Wisconsin", he: "ויסקונסין", ru: "Висконсин" } },
    { code: "MO", name: { en: "Missouri", he: "מיזורי", ru: "Миссури" } },
    { code: "IN", name: { en: "Indiana", he: "אינדיאנה", ru: "Индиана" } },
    { code: "TN", name: { en: "Tennessee", he: "טנסי", ru: "Теннесси" } },
    { code: "OR", name: { en: "Oregon", he: "אורגון", ru: "Орегон" } },
    { code: "LA", name: { en: "Louisiana", he: "לואיזיאנה", ru: "Луизиана" } },
    { code: "KY", name: { en: "Kentucky", he: "קנטקי", ru: "Кентукки" } },
    { code: "OK", name: { en: "Oklahoma", he: "אוקלהומה", ru: "Оклахома" } },
    { code: "NV", name: { en: "Nevada", he: "נבדה", ru: "Невада" } },
    { code: "UT", name: { en: "Utah", he: "יוטה", ru: "Юта" } },
    { code: "NM", name: { en: "New Mexico", he: "ניו מקסיקו", ru: "Нью-Мексико" } },
    { code: "MT", name: { en: "Montana", he: "מונטנה", ru: "Монтана" } },
    { code: "ID", name: { en: "Idaho", he: "איידהו", ru: "Айдахо" } },
    { code: "AK", name: { en: "Alaska", he: "אלסקה", ru: "Аляска" } },
    { code: "HI", name: { en: "Hawaii", he: "הוואי", ru: "Гавайи" } },
    { code: "DC", name: { en: "Washington D.C.", he: "וושינגטון די.סי.", ru: "Вашингтон (округ Колумбия)" } },
  ],
  AU: [
    { code: "NSW", name: { en: "New South Wales", he: "ניו סאות' ויילס", ru: "Новый Южный Уэльс" } },
    { code: "VIC", name: { en: "Victoria", he: "ויקטוריה", ru: "Виктория" } },
    { code: "QLD", name: { en: "Queensland", he: "קווינסלנד", ru: "Квинсленд" } },
    { code: "WA", name: { en: "Western Australia", he: "מערב אוסטרליה", ru: "Западная Австралия" } },
    { code: "SA", name: { en: "South Australia", he: "דרום אוסטרליה", ru: "Южная Австралия" } },
    { code: "TAS", name: { en: "Tasmania", he: "טסמניה", ru: "Тасмания" } },
    { code: "NT", name: { en: "Northern Territory", he: "הטריטוריה הצפונית", ru: "Северная территория" } },
    { code: "ACT", name: { en: "Australian Capital Territory", he: "טריטוריית הבירה", ru: "Столичная территория" } },
  ],
  CA: [
    { code: "ON", name: { en: "Ontario", he: "אונטריו", ru: "Онтарио" } },
    { code: "QC", name: { en: "Quebec", he: "קוויבק", ru: "Квебек" } },
    { code: "BC", name: { en: "British Columbia", he: "בריטיש קולומביה", ru: "Британская Колумбия" } },
    { code: "AB", name: { en: "Alberta", he: "אלברטה", ru: "Альберта" } },
    { code: "SK", name: { en: "Saskatchewan", he: "ססקצ'ואן", ru: "Саскачеван" } },
    { code: "MB", name: { en: "Manitoba", he: "מניטובה", ru: "Манитоба" } },
    { code: "NS", name: { en: "Nova Scotia", he: "נובה סקוטיה", ru: "Новая Шотландия" } },
    { code: "NB", name: { en: "New Brunswick", he: "ניו ברנזוויק", ru: "Нью-Брансуик" } },
    { code: "NL", name: { en: "Newfoundland", he: "ניופאונדלנד", ru: "Ньюфаундленд" } },
    { code: "PE", name: { en: "Prince Edward Island", he: "אי הנסיך אדוארד", ru: "Остров Принца Эдуарда" } },
  ],
  RU: [
    { code: "MOW", name: { en: "Moscow", he: "מוסקבה", ru: "Москва" } },
    { code: "SPE", name: { en: "Saint Petersburg", he: "סנט פטרסבורג", ru: "Санкт-Петербург" } },
    { code: "KDA", name: { en: "Krasnodar", he: "קרסנודר", ru: "Краснодар" } },
    { code: "SAM", name: { en: "Samara", he: "סמרה", ru: "Самара" } },
    { code: "SVE", name: { en: "Sverdlovsk", he: "סברדלובסק", ru: "Свердловская область" } },
    { code: "NVS", name: { en: "Novosibirsk", he: "נובוסיבירסק", ru: "Новосибирск" } },
    { code: "IRK", name: { en: "Irkutsk", he: "אירקוטסק", ru: "Иркутск" } },
    { code: "PRI", name: { en: "Primorsky", he: "פרימורסקי", ru: "Приморский край" } },
    { code: "KAM", name: { en: "Kamchatka", he: "קמצ'טקה", ru: "Камчатка" } },
  ],
  BR: [
    { code: "SP", name: { en: "São Paulo", he: "סאו פאולו", ru: "Сан-Паулу" } },
    { code: "RJ", name: { en: "Rio de Janeiro", he: "ריו דה ז'נירו", ru: "Рио-де-Жанейро" } },
    { code: "MG", name: { en: "Minas Gerais", he: "מינאס ז'ראיס", ru: "Минас-Жерайс" } },
    { code: "BA", name: { en: "Bahia", he: "באהיה", ru: "Баия" } },
    { code: "AM", name: { en: "Amazonas", he: "אמזונס", ru: "Амазонас" } },
    { code: "PA", name: { en: "Pará", he: "פארה", ru: "Пара" } },
    { code: "RS", name: { en: "Rio Grande do Sul", he: "ריו גרנדה דו סול", ru: "Риу-Гранди-ду-Сул" } },
    { code: "PR", name: { en: "Paraná", he: "פרנה", ru: "Парана" } },
    { code: "MT", name: { en: "Mato Grosso", he: "מאטו גרוסו", ru: "Мату-Гросу" } },
    { code: "AC", name: { en: "Acre", he: "אקרי", ru: "Акри" } },
  ],
  MX: [
    { code: "CMX", name: { en: "Mexico City", he: "מקסיקו סיטי", ru: "Мехико" } },
    { code: "JAL", name: { en: "Jalisco", he: "חליסקו", ru: "Халиско" } },
    { code: "NLE", name: { en: "Nuevo León", he: "נואבו לאון", ru: "Нуэво-Леон" } },
    { code: "BCN", name: { en: "Baja California", he: "באחה קליפורניה", ru: "Нижняя Калифорния" } },
    { code: "BCS", name: { en: "Baja California Sur", he: "באחה קליפורניה סור", ru: "Южная Нижняя Калифорния" } },
    { code: "SIN", name: { en: "Sinaloa", he: "סינלואה", ru: "Синалоа" } },
    { code: "SON", name: { en: "Sonora", he: "סונורה", ru: "Сонора" } },
    { code: "CHH", name: { en: "Chihuahua", he: "צ'יוואווה", ru: "Чиуауа" } },
  ],
  ID: [
    { code: "JK", name: { en: "Jakarta", he: "ג'קרטה", ru: "Джакарта" } },
    { code: "JB", name: { en: "West Java", he: "מערב ג'אווה", ru: "Западная Ява" } },
    { code: "JT", name: { en: "Central Java", he: "מרכז ג'אווה", ru: "Центральная Ява" } },
    { code: "JI", name: { en: "East Java", he: "מזרח ג'אווה", ru: "Восточная Ява" } },
    { code: "KT", name: { en: "East Kalimantan", he: "מזרח קלימנטן", ru: "Восточный Калимантан" } },
    { code: "BA", name: { en: "Bali", he: "באלי", ru: "Бали" } },
    { code: "PA", name: { en: "Papua", he: "פפואה", ru: "Папуа" } },
  ],
  CL: [
    { code: "RM", name: { en: "Santiago Metropolitan", he: "סנטיאגו", ru: "Сантьяго" } },
    { code: "VS", name: { en: "Valparaíso", he: "ולפראיסו", ru: "Вальпараисо" } },
    { code: "BI", name: { en: "Biobío", he: "ביוביו", ru: "Биобио" } },
    { code: "IP", name: { en: "Easter Island", he: "אי הפסחא", ru: "Остров Пасхи" } },
  ],
  KZ: [
    { code: "ALA", name: { en: "Almaty", he: "אלמטי", ru: "Алматы" } },
    { code: "AST", name: { en: "Astana", he: "אסטנה", ru: "Астана" } },
    { code: "AKT", name: { en: "Aktau", he: "אקטאו", ru: "Актау" } },
    { code: "ATY", name: { en: "Aktobe", he: "אקטובה", ru: "Актобе" } },
  ],
  CN: [
    { code: "XJ", name: { en: "Xinjiang", he: "שינג'יאנג", ru: "Синьцзян" } },
  ],
  MN: [
    { code: "UB", name: { en: "Ulaanbaatar", he: "אולן בטור", ru: "Улан-Батор" } },
    { code: "HOV", name: { en: "Khovd", he: "חובד", ru: "Ховд" } },
  ],
  CD: [
    { code: "KN", name: { en: "Kinshasa", he: "קינשאסה", ru: "Киншаса" } },
    { code: "LB", name: { en: "Lubumbashi", he: "לובומבשי", ru: "Лубумбаши" } },
  ],
  AR: [
    { code: "BA", name: { en: "Buenos Aires", he: "בואנוס איירס", ru: "Буэнос-Айрес" } },
    { code: "CF", name: { en: "Capital Federal", he: "בירת הפדרציה", ru: "Федеральная столица" } },
    { code: "SL", name: { en: "San Luis", he: "סן לואיס", ru: "Сан-Луис" } },
  ],
  PT: [
    { code: "30", name: { en: "Azores", he: "האיים האזוריים", ru: "Азорские острова" } },
    { code: "20", name: { en: "Madeira", he: "מדיירה", ru: "Мадейра" } },
  ],
  ES: [
    { code: "CN", name: { en: "Canary Islands", he: "האיים הקנריים", ru: "Канарские острова" } },
  ],
  NZ: [
    { code: "CIT", name: { en: "Chatham Islands", he: "איי צ'תם", ru: "Острова Чатем" } },
  ],
};

/**
 * Get the localized name for a country based on the current locale.
 */
export function getCountryName(code: string, locale: string): string {
  const country = COUNTRIES.find(c => c.code === code);
  if (!country) return code;
  const lang = locale.startsWith("he") ? "he" : locale.startsWith("ru") ? "ru" : "en";
  return country.name[lang];
}

/**
 * Get the localized name for a state based on the current locale.
 */
export function getStateName(countryCode: string, stateCode: string, locale: string): string {
  const states = STATES[countryCode];
  if (!states) return stateCode;
  const state = states.find(s => s.code === stateCode);
  if (!state) return stateCode;
  const lang = locale.startsWith("he") ? "he" : locale.startsWith("ru") ? "ru" : "en";
  return state.name[lang];
}
