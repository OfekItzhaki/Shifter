"use client";

import { useEffect } from "react";

declare global {
  interface Window {
    $crisp?: unknown[];
    CRISP_WEBSITE_ID?: string;
  }
}

interface CrispChatProps {
  websiteId: string;
}

export default function CrispChat({ websiteId }: CrispChatProps) {
  useEffect(() => {
    if (!websiteId || document.querySelector('script[src="https://client.crisp.chat/l.js"]')) {
      return;
    }

    window.$crisp = window.$crisp ?? [];
    window.CRISP_WEBSITE_ID = websiteId;

    const script = document.createElement("script");
    script.src = "https://client.crisp.chat/l.js";
    script.async = true;
    document.head.appendChild(script);
  }, [websiteId]);

  return null;
}
