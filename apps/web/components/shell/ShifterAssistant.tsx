"use client";

import { FormEvent, useMemo, useRef, useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { usePathname, useRouter } from "next/navigation";
import { sendAiChatMessage, type AiChatAction, type AiChatMessage } from "@/lib/api/ai";
import { useAuthStore } from "@/lib/store/authStore";
import { useSpaceStore } from "@/lib/store/spaceStore";
import { openFeedbackModal } from "@/components/shell/FeedbackFab";

interface AssistantMessage extends AiChatMessage {
  id: string;
  actions?: AiChatAction[];
}

function newId() {
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

export default function ShifterAssistant() {
  const t = useTranslations("assistant");
  const locale = useLocale();
  const pathname = usePathname();
  const router = useRouter();
  const currentSpaceId = useSpaceStore((s) => s.currentSpaceId);
  const adminGroupId = useAuthStore((s) => s.adminGroupId);
  const [open, setOpen] = useState(false);
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const [messages, setMessages] = useState<AssistantMessage[]>(() => [
    {
      id: "welcome",
      role: "assistant",
      content: t("welcome"),
    },
  ]);
  const inputRef = useRef<HTMLTextAreaElement>(null);

  const quickPrompts = useMemo(
    () => [t("quick.import"), t("quick.schedule"), t("quick.support")],
    [t]
  );

  async function send(message: string) {
    const trimmed = message.trim();
    if (!trimmed || loading) return;

    if (!currentSpaceId) {
      setMessages((prev) => [
        ...prev,
        { id: newId(), role: "user", content: trimmed },
        { id: newId(), role: "assistant", content: t("noSpace") },
      ]);
      setInput("");
      return;
    }

    const userMessage: AssistantMessage = { id: newId(), role: "user", content: trimmed };
    const recentMessages = [...messages, userMessage]
      .filter((m) => m.role === "user" || m.role === "assistant")
      .slice(-12)
      .map(({ role, content }) => ({ role, content }));

    setMessages((prev) => [...prev, userMessage]);
    setInput("");
    setLoading(true);

    try {
      const response = await sendAiChatMessage(currentSpaceId, {
        message: trimmed,
        locale,
        currentPath: pathname,
        isAdminMode: Boolean(adminGroupId),
        recentMessages,
      });

      setMessages((prev) => [
        ...prev,
        {
          id: newId(),
          role: "assistant",
          content: response.message || t("fallback"),
          actions: response.suggestedActions ?? [],
        },
      ]);
    } catch {
      setMessages((prev) => [
        ...prev,
        {
          id: newId(),
          role: "assistant",
          content: t("fallback"),
          actions: [{ type: "feedback", label: t("actions.feedback"), payload: null }],
        },
      ]);
    } finally {
      setLoading(false);
    }
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    void send(input);
  }

  function handleAction(action: AiChatAction) {
    if (action.type === "feedback") {
      openFeedbackModal({ type: "feedback", initialDescription: messages.at(-1)?.content });
      return;
    }

    if (action.type === "contact") {
      openFeedbackModal({ type: "feedback", initialDescription: action.payload ?? undefined });
      return;
    }

    if (action.type === "open_path" && action.payload?.startsWith("/")) {
      router.push(action.payload);
      setOpen(false);
    }
  }

  function openAssistant() {
    setOpen(true);
    window.setTimeout(() => inputRef.current?.focus(), 0);
  }

  return (
    <>
      <button
        type="button"
        onClick={openAssistant}
        className="fixed bottom-24 right-5 z-[1190] flex h-12 w-12 items-center justify-center rounded-2xl border border-sky-300/40 bg-sky-500 text-white shadow-xl shadow-sky-900/20 transition-colors hover:bg-sky-600 dark:border-sky-400/30"
        aria-label={t("open")}
      >
        <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.9} strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
          <path d="M12 3l1.9 4.1L18 9l-4.1 1.9L12 15l-1.9-4.1L6 9l4.1-1.9L12 3z" />
          <path d="M19 15l.9 2.1L22 18l-2.1.9L19 21l-.9-2.1L16 18l2.1-.9L19 15z" />
          <path d="M5 14l.7 1.6L7 16l-1.3.4L5 18l-.7-1.6L3 16l1.3-.4L5 14z" />
        </svg>
      </button>

      {open && (
        <div className="fixed inset-0 z-[1300] bg-slate-950/30 p-3 backdrop-blur-sm sm:flex sm:items-end sm:justify-end">
          <div className="flex h-full w-full flex-col overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-2xl dark:border-slate-700 dark:bg-slate-900 sm:h-[620px] sm:max-w-md">
            <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-700">
              <div>
                <p className="text-sm font-semibold text-slate-900 dark:text-white">{t("title")}</p>
                <p className="text-xs text-slate-500 dark:text-slate-400">{t("subtitle")}</p>
              </div>
              <button
                type="button"
                onClick={() => setOpen(false)}
                aria-label={t("close")}
                className="rounded-xl px-3 py-2 text-sm font-semibold text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-slate-800 dark:hover:text-white"
              >
                x
              </button>
            </div>

            <div className="flex-1 space-y-3 overflow-y-auto px-4 py-4">
              {messages.map((message) => (
                <div key={message.id} className={message.role === "user" ? "text-end" : "text-start"}>
                  <div
                    className={`inline-block max-w-[86%] rounded-2xl px-3 py-2 text-sm leading-6 ${
                      message.role === "user"
                        ? "bg-sky-500 text-white"
                        : "bg-slate-100 text-slate-800 dark:bg-slate-800 dark:text-slate-100"
                    }`}
                  >
                    {message.content}
                  </div>
                  {message.actions && message.actions.length > 0 && (
                    <div className="mt-2 flex flex-wrap gap-2">
                      {message.actions.map((action, index) => (
                        <button
                          key={`${action.type}-${index}`}
                          type="button"
                          onClick={() => handleAction(action)}
                          className="rounded-xl border border-slate-200 px-3 py-1.5 text-xs font-semibold text-slate-600 transition-colors hover:border-sky-300 hover:text-sky-700 dark:border-slate-700 dark:text-slate-300 dark:hover:border-sky-600 dark:hover:text-sky-300"
                        >
                          {action.label}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              ))}
              {loading && (
                <div className="text-start">
                  <div className="inline-block rounded-2xl bg-slate-100 px-3 py-2 text-sm text-slate-500 dark:bg-slate-800 dark:text-slate-300">
                    {t("thinking")}
                  </div>
                </div>
              )}
            </div>

            <div className="border-t border-slate-200 p-3 dark:border-slate-700">
              <div className="mb-2 flex gap-2 overflow-x-auto pb-1">
                {quickPrompts.map((prompt) => (
                  <button
                    key={prompt}
                    type="button"
                    onClick={() => void send(prompt)}
                    className="shrink-0 rounded-full border border-slate-200 px-3 py-1.5 text-xs text-slate-600 transition-colors hover:border-sky-300 hover:text-sky-700 dark:border-slate-700 dark:text-slate-300 dark:hover:border-sky-600 dark:hover:text-sky-300"
                  >
                    {prompt}
                  </button>
                ))}
              </div>
              <form onSubmit={handleSubmit} className="flex items-end gap-2">
                <textarea
                  ref={inputRef}
                  value={input}
                  onChange={(event) => setInput(event.target.value)}
                  placeholder={t("placeholder")}
                  rows={2}
                  maxLength={2000}
                  className="min-h-[48px] flex-1 resize-none rounded-2xl border border-slate-200 bg-white px-3 py-2 text-sm text-slate-900 outline-none transition-colors focus:border-sky-400 dark:border-slate-700 dark:bg-slate-950 dark:text-white"
                />
                <button
                  type="submit"
                  disabled={loading || !input.trim()}
                  className="rounded-2xl bg-sky-500 px-4 py-3 text-sm font-semibold text-white transition-colors hover:bg-sky-600 disabled:cursor-not-allowed disabled:bg-slate-200 disabled:text-slate-400 dark:disabled:bg-slate-800"
                >
                  {t("send")}
                </button>
              </form>
            </div>
          </div>
        </div>
      )}
    </>
  );
}
