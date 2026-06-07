type Translator = (key: string) => string;

export interface NotificationText {
  title: string;
  body: string;
}

export function getNotificationText(
  t: Translator,
  eventType: string,
  fallback: NotificationText
): NotificationText {
  const key = eventType.startsWith("self_service.")
    ? `events.${eventType}`
    : `events.${eventType.replace(/\./g, "_")}`;

  return {
    title: translateOrFallback(t, `${key}.title`, fallback.title),
    body: translateOrFallback(t, `${key}.body`, fallback.body),
  };
}

function translateOrFallback(t: Translator, key: string, fallback: string): string {
  try {
    return t(key);
  } catch {
    return fallback;
  }
}
