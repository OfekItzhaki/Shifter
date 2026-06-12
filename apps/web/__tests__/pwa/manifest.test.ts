import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";

type ManifestIcon = {
  src: string;
  sizes: string;
  type: string;
  purpose?: string;
};

type ManifestShortcut = {
  name: string;
  short_name: string;
  description: string;
  url: string;
  icons: ManifestIcon[];
};

type WebAppManifest = {
  name: string;
  short_name: string;
  start_url: string;
  scope: string;
  display: string;
  icons: ManifestIcon[];
  shortcuts?: ManifestShortcut[];
};

function readManifest(): WebAppManifest {
  const manifestPath = path.join(process.cwd(), "public", "manifest.json");
  return JSON.parse(readFileSync(manifestPath, "utf8")) as WebAppManifest;
}

function publicAssetExists(src: string): boolean {
  return existsSync(path.join(process.cwd(), "public", src.replace(/^\//, "")));
}

describe("PWA manifest", () => {
  it("keeps the app installable with existing icon assets", () => {
    const manifest = readManifest();

    expect(manifest.name).toContain("Shifter");
    expect(manifest.short_name).toBe("Shifter");
    expect(manifest.start_url).toBe("/schedule/my-missions");
    expect(manifest.scope).toBe("/");
    expect(manifest.display).toBe("standalone");

    expect(manifest.icons.length).toBeGreaterThanOrEqual(2);
    for (const icon of manifest.icons) {
      expect(icon.src).toMatch(/^\//);
      expect(icon.type).toBe("image/png");
      expect(publicAssetExists(icon.src)).toBe(true);
    }
  });

  it("exposes installed-app shortcuts for core member flows", () => {
    const manifest = readManifest();
    const shortcuts = manifest.shortcuts ?? [];

    expect(shortcuts.map((shortcut) => shortcut.url)).toEqual([
      "/schedule/my-missions",
      "/pick",
      "/profile",
    ]);

    for (const shortcut of shortcuts) {
      expect(shortcut.name).toBeTruthy();
      expect(shortcut.short_name).toBeTruthy();
      expect(shortcut.description).toBeTruthy();
      expect(shortcut.icons.length).toBeGreaterThan(0);
      expect(shortcut.url).toMatch(/^\//);

      for (const icon of shortcut.icons) {
        expect(icon.src).toMatch(/^\//);
        expect(icon.type).toBe("image/png");
        expect(publicAssetExists(icon.src)).toBe(true);
      }
    }
  });
});
