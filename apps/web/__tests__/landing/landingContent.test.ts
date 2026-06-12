import { describe, expect, it } from "vitest";
import { LANDING_CONTENT } from "@/app/landingContent";

describe("landing PWA copy", () => {
  it("describes PWA install support as phone and desktop capable", () => {
    expect(LANDING_CONTENT.en.hero.install).toContain("supported desktop browsers");
    expect(LANDING_CONTENT.ru.hero.install).toContain("supported desktop browsers");
    expect(LANDING_CONTENT.he.hero.install).toContain("דסקטופ");

    const enDownloadAnswer = LANDING_CONTENT.en.faq.items.find((item) =>
      item.q.includes("download")
    )?.a;
    const heDownloadAnswer = LANDING_CONTENT.he.faq.items.find((item) =>
      item.q.includes("להוריד")
    )?.a;
    const ruDownloadAnswer = LANDING_CONTENT.ru.faq.items.find((item) =>
      item.q.includes("download")
    )?.a;

    expect(enDownloadAnswer).toContain("supported desktop browsers");
    expect(ruDownloadAnswer).toContain("supported desktop browsers");
    expect(heDownloadAnswer).toContain("דסקטופ");
  });

  it("does not regress to phone-only install messaging", () => {
    const serializedContent = JSON.stringify(LANDING_CONTENT);

    expect(serializedContent).not.toContain("Installable on iPhone and Android");
    expect(serializedContent).not.toContain("PWA on iPhone and Android");
    expect(serializedContent).not.toContain("באייפון ובאנדרואיד כ-PWA");
  });
});
