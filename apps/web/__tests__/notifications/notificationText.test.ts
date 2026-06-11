import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { getNotificationText } from "../../lib/notifications/messageText";

const SELF_SERVICE_NOTIFICATION_EVENTS = [
  "request_approved",
  "request_rejected",
  "absence_reported",
  "absence_approved",
  "absence_rejected",
  "change_requested",
  "change_cancelled",
  "change_approved",
  "change_rejected",
  "special_leave_requested",
  "special_leave_cancelled",
  "special_leave_approved",
  "special_leave_rejected",
  "admin_assigned",
  "admin_removed",
  "swap_proposal_received",
  "swap_accepted",
  "swap_declined",
  "swap_cancelled",
  "swap_expired",
  "waitlist_offer",
  "waitlist_offer_expired",
  "under_scheduled_members",
  "under_scheduled_warning",
] as const;

const LOCALES = ["en", "he", "ru"] as const;

describe("notification text", () => {
  it("has text for every persisted self-service notification event in each locale", () => {
    for (const locale of LOCALES) {
      const messages = JSON.parse(
        readFileSync(join(process.cwd(), "messages", `${locale}.json`), "utf8")
      );
      const selfServiceEvents = messages.notifications.events.self_service;

      for (const event of SELF_SERVICE_NOTIFICATION_EVENTS) {
        expect(selfServiceEvents[event]?.title, `${locale}.${event}.title`).toEqual(expect.any(String));
        expect(selfServiceEvents[event]?.title.length, `${locale}.${event}.title length`).toBeGreaterThan(0);
        expect(selfServiceEvents[event]?.body, `${locale}.${event}.body`).toEqual(expect.any(String));
        expect(selfServiceEvents[event]?.body.length, `${locale}.${event}.body length`).toBeGreaterThan(0);
      }
    }
  });

  it("uses the self-service event translation before falling back", () => {
    const translations: Record<string, string> = {
      "events.self_service.waitlist_offer_expired.title": "Translated title",
      "events.self_service.waitlist_offer_expired.body": "Translated body",
    };

    const text = getNotificationText(
      (key) => translations[key] ?? key,
      "self_service.waitlist_offer_expired",
      { title: "Fallback title", body: "Fallback body" }
    );

    expect(text).toEqual({
      title: "Translated title",
      body: "Translated body",
    });
  });
});
