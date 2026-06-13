import { describe, expect, it } from "vitest";
import { LANDING_CONTENT } from "@/app/landingContent";

describe("landing content", () => {
  it("describes PWA install support as phone and desktop capable", () => {
    expect(LANDING_CONTENT.en.hero.install).toContain("supported desktop browsers");
    expect(LANDING_CONTENT.ru.hero.install).toContain("desktop-браузерах");
    expect(LANDING_CONTENT.he.hero.install).toContain("דסקטופ");

    const enDownloadAnswer = LANDING_CONTENT.en.faq.items.find((item) =>
      item.q.includes("download")
    )?.a;
    const heDownloadAnswer = LANDING_CONTENT.he.faq.items.find((item) =>
      item.q.includes("להוריד")
    )?.a;
    const ruDownloadAnswer = LANDING_CONTENT.ru.faq.items.find((item) =>
      item.q.includes("скачивать")
    )?.a;

    expect(enDownloadAnswer).toContain("supported desktop browsers");
    expect(ruDownloadAnswer).toContain("desktop-браузерах");
    expect(heDownloadAnswer).toContain("דסקטופ");
  });

  it("does not regress to phone-only install messaging", () => {
    const serializedContent = JSON.stringify(LANDING_CONTENT);

    expect(serializedContent).not.toContain("Installable on iPhone and Android");
    expect(serializedContent).not.toContain("PWA on iPhone and Android");
    expect(serializedContent).not.toContain("באייפון ובאנדרואיד כ-PWA");
  });

  it("localizes the finder and real app preview", () => {
    expect(LANDING_CONTENT.en.finder.items[0].label).toBe("Manual self-service");
    expect(LANDING_CONTENT.he.finder.items[0].label).toBe("שירות עצמי ידני");
    expect(LANDING_CONTENT.ru.finder.items[0].label).toBe("Ручное самообслуживание");

    expect(LANDING_CONTENT.en.preview.publishLabel).toBe("Publish");
    expect(LANDING_CONTENT.he.preview.publishLabel).toBe("פרסום");
    expect(LANDING_CONTENT.ru.preview.publishLabel).toBe("Опубликовать");
  });

  it("explains manual self-service as a second scheduling mode", () => {
    expect(LANDING_CONTENT.en.faq.items[0].a).toContain("second scheduling mode");
    expect(LANDING_CONTENT.he.faq.items[0].a).toContain("מצב סידור שני");
    expect(LANDING_CONTENT.ru.faq.items[0].a).toContain("второй режим планирования");
  });
});
