"use client";

import Modal from "@/components/Modal";

interface Message {
  id: string;
  content: string;
  authorName: string;
  createdAt: string;
  isPinned: boolean;
}

interface Props {
  isAdmin: boolean;
  messages: Message[];
  messagesLoading: boolean;
  messagesError: string | null;
  newMessageContent: string;
  messageSending: boolean;
  messageError: string | null;
  messagePinErrors: Record<string, string>;
  editingMessageId: string | null;
  editMessageContent: string;
  editMessageSaving: boolean;
  editMessageError: string | null;
  onNewMessageChange: (v: string) => void;
  onSendMessage: (e: React.FormEvent) => void;
  onPinMessage: (id: string, isPinned: boolean) => void;
  onStartEditMessage: (id: string, content: string) => void;
  onCloseEditMessage: () => void;
  onEditMessageContentChange: (v: string) => void;
  onUpdateMessage: (id: string) => void;
  onDeleteMessage: (id: string) => void;
}

const INP = "border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500";

function LoadingSpinner() {
  return (
    <div className="flex items-center gap-3 text-slate-400 text-sm py-8">
      <svg className="animate-spin h-5 w-5 text-blue-400" fill="none" viewBox="0 0 24 24">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
      </svg>
      טוען...
    </div>
  );
}

export default function MessagesTab({
  isAdmin, messages, messagesLoading, messagesError,
  newMessageContent, messageSending, messageError, messagePinErrors,
  editingMessageId, editMessageContent, editMessageSaving, editMessageError,
  onNewMessageChange, onSendMessage, onPinMessage,
  onStartEditMessage, onCloseEditMessage, onEditMessageContentChange,
  onUpdateMessage, onDeleteMessage,
}: Props) {
  return (
    <div className="space-y-4">
      <form onSubmit={onSendMessage} className="flex gap-2">
        <input
          value={newMessageContent}
          onChange={e => onNewMessageChange(e.target.value)}
          placeholder="כתוב הודעה לקבוצה..."
          className={`flex-1 ${INP}`}
        />
        <button
          type="submit"
          disabled={messageSending || !newMessageContent.trim()}
          className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors whitespace-nowrap"
        >
          {messageSending ? "שולח..." : "שלח"}
        </button>
      </form>
      {messageError && <p className="text-sm text-red-600">{messageError}</p>}

      {messagesLoading ? (
        <LoadingSpinner />
      ) : messagesError ? (
        <p className="text-sm text-red-600 py-4">{messagesError}</p>
      ) : messages.length === 0 ? (
        <p className="text-sm text-slate-400 py-8 text-center">אין הודעות עדיין. היה הראשון לכתוב!</p>
      ) : (
        <div className="space-y-3">
          {[...messages].reverse().map(msg => (
            <div key={msg.id} className={`rounded-2xl border p-4 ${msg.isPinned ? "bg-amber-50 border-amber-200 shadow-sm" : "bg-white border-slate-200"}`}>
              <div className="flex items-start gap-3">
                <div className="w-8 h-8 rounded-full bg-blue-50 flex items-center justify-center text-blue-600 text-sm font-semibold flex-shrink-0">
                  {msg.authorName?.charAt(0)?.toUpperCase() ?? "?"}
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between gap-2 mb-1">
                    <div className="flex items-center gap-2">
                      <span className="text-sm font-semibold text-slate-900">{msg.authorName}</span>
                      {msg.isPinned && (
                        <span className="text-xs text-amber-700 bg-amber-100 px-1.5 py-0.5 rounded-full">📌 נעוץ</span>
                      )}
                      <span className="text-xs text-slate-400">
                        {new Date(msg.createdAt).toLocaleString("he-IL", { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" })}
                      </span>
                    </div>
                    {isAdmin && (
                      <div className="flex items-center gap-1.5 flex-shrink-0">
                        <button
                          onClick={() => onPinMessage(msg.id, !msg.isPinned)}
                          className={`text-xs border px-2 py-1 rounded-lg transition-colors ${msg.isPinned ? "text-amber-600 border-amber-200 hover:bg-amber-50" : "text-slate-500 border-slate-200 hover:bg-slate-50"}`}
                        >
                          {msg.isPinned ? "📌 בטל" : "📌 נעץ"}
                        </button>
                        <button
                          onClick={() => onStartEditMessage(msg.id, msg.content)}
                          className="text-xs text-blue-500 hover:text-blue-700 border border-blue-200 hover:border-blue-400 px-2 py-1 rounded-lg transition-colors"
                        >
                          ערוך
                        </button>
                        <button
                          onClick={() => { if (confirm("האם אתה בטוח שברצונך למחוק הודעה זו?")) onDeleteMessage(msg.id); }}
                          className="text-xs text-red-500 hover:text-red-700 border border-red-200 hover:border-red-400 px-2 py-1 rounded-lg transition-colors"
                        >
                          מחק
                        </button>
                      </div>
                    )}
                  </div>
                  <p className="text-sm text-slate-700 whitespace-pre-wrap">{msg.content}</p>
                  {messagePinErrors[msg.id] && (
                    <p className="text-xs text-red-600 mt-1">{messagePinErrors[msg.id]}</p>
                  )}
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Edit message modal */}
      <Modal title="עריכת הודעה" open={!!editingMessageId} onClose={onCloseEditMessage}>
        <div className="space-y-3">
          <textarea
            value={editMessageContent}
            onChange={e => onEditMessageContentChange(e.target.value)}
            rows={4}
            className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
          />
          {editMessageError && <p className="text-xs text-red-600">{editMessageError}</p>}
          <div className="flex gap-2">
            <button
              onClick={() => editingMessageId && onUpdateMessage(editingMessageId)}
              disabled={editMessageSaving}
              className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors"
            >
              {editMessageSaving ? "שומר..." : "שמור"}
            </button>
            <button onClick={onCloseEditMessage} className="text-xs text-slate-500 hover:text-slate-700 px-2">ביטול</button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
