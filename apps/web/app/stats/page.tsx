"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import AppShell from "@/components/shell/AppShell";
import { useSpaceStore } from "@/lib/store/spaceStore";

export default function StatsPage() {
  const t = useTranslations("stats");
  const router = useRouter();
  const currentSpaceId = useSpaceStore((s) => s.currentSpaceId);

  // Redirect to groups page — stats now live inside each group's tab
  useEffect(() => {
    router.replace("/groups");
  }, [router]);

  return (
    <AppShell>
      <div className="max-w-5xl mx-auto">
        <div className="text-center py-20">
          <p className="text-slate-500 dark:text-slate-400">
            הסטטיסטיקות עברו לתוך כל קבוצה — עבור לקבוצה הרצויה ולחץ על טאב &quot;סטטיסטיקה&quot;.
          </p>
        </div>
      </div>
    </AppShell>
  );
}
