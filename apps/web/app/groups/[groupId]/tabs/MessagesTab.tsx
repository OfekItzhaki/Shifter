"use client";

import { useState } from "react";

interface Message { id: string; content: string; authorName: string; createdAt: string; isPinned: boolean; }

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

export default function MessagesTab({
  isAdmin, messages, messagesLoading, messagesError,
  newMessageContent, messageSending, messageError, messagePinErrors,
  editingMessageId, editMessageContent, editMessageSaving, editMessageError,
  onNewMessageChange, onSendMessage, onPinMessage,
  onStartEditMessage, onCloseEditMessage, onEditMessageContentChange, onUpdateMessage, onDeleteMessage,
}: Props) {
  const pinned = messages.filter(m => m.isPinned);
  const regular = messages.filter(m => !m.isPinned);

  return (
    <div className="space-y-4">
      {/* Compose */}
      <form onSubmit={onSendMessage} className="flex gap-2">
        <input
          type="text"
          value={newMessageContent}
          onChange={e => onNewMessageChange(e.target.value)}
          placeholder="כתוב הודעה..."
          className="flex-1 border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
        <button type="submit" disabled={messageSending || !newMessageContent.trim()} className="bg-blue-500 hover:bg-blue-600 text-white text-sm font-medium px-4 py-2.5 rounded-xl disabled:opacity-50 transition-colors">
          {messageSending ? "שולח..." : "שלח"}
        </button>
      </form>
      {messageError && <p className="text-sm text-red-600">{messageError}</p>}

      {messagesLoading && <p className="text-sm text-slate-400 py-8">טוען הודעות...</p>}
      {messagesError && <p className="text-sm text-red-600">{messagesError}</p>}

      {/* Pinned */}
      {pinned.length > 0 && (
        <div className="space-y-2">
          <h3 className="text-xs font-semibold text-slate-500 uppercase tracking-wider">📌 נעוצות</h3>
          {pinned.map(m => <MessageCard key={m.id} message={m} isAdmin={isAdmin} editingId={editingMessageId} editContent={editMessageContent} editSaving={editMessageSaving} editError={editMessageError} pinErrors={messagePinErrors} onPin={onPinMessage} onStartEdit={onStartEditMessage} onCloseEdit={onCloseEditMessage} onEditChange={onEditMessageContentChange} onUpdate={onUpdateMessage} onDelete={onDeleteMessage} />)}
        </div>
      )}

      {/* Regular */}
      {regular.length === 0 && !messagesLoading && pinned.length === 0 && (
        <div className="flex flex-col items-center justify-center py-16 text-center bg-white rounded-xl border border-slate-200">
          <p className="text-slate-400 text-sm">אין הודעות עדיין</p>
        </div>
      )}
      <div className="space-y-2">
        {regular.map(m => <MessageCard key={m.id} message={m} isAdmin={isAdmin} editingId={editingMessageId} editContent={editMessageContent} editSaving={editMessageSaving} editError={editMessageError} pinErrors={messagePinErrors} onPin={onPinMessage} onStartEdit={onStartEditMessage} onCloseEdit={onCloseEditMessage} onEditChange={onEditMessageContentChange} onUpdate={onUpdateMessage} onDelete={onDeleteMessage} />)}
      </div>
    </div>
  );
}

function MessageCard({ message: m, isAdmin, editingId, editContent, editSaving, editError, pinErrors, onPin, onStartEdit, onCloseEdit, onEditChange, onUpdate, onDelete }: {
  message: Message; isAdmin: boolean; editingId: string | null; editContent: string; editSaving: boolean; editError: string | null; pinErrors: Record<string, string>;
  onPin: (id: string, v: boolean) => void; onStartEdit: (id: string, c: string) => void; onCloseEdit: () => void; onEditChange: (v: string) => void; onUpdate: (id: string) => void; onDelete: (id: string) => void;
}) {
  const [confirmDelete, setConfirmDelete] = useState(false);
  return (
    <div className={`bg-white border rounded-2xl p-4 space-y-2 ${m.isPinned ? "border-amber-200 bg-amber-50/30" : "border-slate-200"}`}>
      {editingId === m.id ? (
        <div className="space-y-2">
          <textarea value={editContent} onChange={e => onEditChange(e.target.value)} rows={3} className="w-full border border-slate-200 rounded-xl px-3.5 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none" />
          {editError && <p className="text-xs text-red-600">{editError}</p>}
          <div className="flex gap-2">
            <button onClick={() => onUpdate(m.id)} disabled={editSaving} className="bg-blue-500 hover:bg-blue-600 text-white text-xs font-medium px-3 py-1.5 rounded-lg disabled:opacity-50 transition-colors">{editSaving ? "שומר..." : "שמור"}</button>
            <button onClick={onCloseEdit} className="text-xs text-slate-500 border border-slate-200 px-3 py-1.5 rounded-lg hover:bg-slate-50 transition-colors">ביטול</button>
          </div>
        </div>
      ) : (
        <>
          <div className="flex items-start justify-between gap-2">
            <div>
              <span className="text-xs font-semibold text-slate-500">{m.authorName}</span>
              <p className="text-sm text-slate-800 mt-0.5">{m.content}</p>
            </div>
            {isAdmin && (
              <div className="flex gap-1.5 flex-shrink-0 flex-wrap justify-end">
                <button onClick={() => onPin(m.id, !m.isPinned)} className="text-xs text-slate-500 hover:text-amber-600 border border-slate-200 px-2 py-1 rounded-lg hover:bg-amber-50 transition-colors">{m.isPinned ? "בטל נעיצה" : "נעץ"}</button>
                <button onClick={() => onStartEdit(m.id, m.content)} className="text-xs text-slate-500 hover:text-slate-700 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">ערוך</button>
                {confirmDelete ? (
                  <>
                    <span className="text-xs text-slate-600">למחוק?</span>
                    <button onClick={() => { setConfirmDelete(false); onDelete(m.id); }} className="text-xs text-white bg-red-500 hover:bg-red-600 px-2 py-1 rounded-lg transition-colors">אישור</button>
                    <button onClick={() => setConfirmDelete(false)} className="text-xs text-slate-500 border border-slate-200 px-2 py-1 rounded-lg hover:bg-slate-50 transition-colors">ביטול</button>
                  </>
                ) : (
                  <button onClick={() => setConfirmDelete(true)} className="text-xs text-red-500 hover:text-red-700 border border-red-100 px-2 py-1 rounded-lg hover:bg-red-50 transition-colors">מחק</button>
                )}
              </div>
            )}
          </div>
          {pinErrors[m.id] && <p className="text-xs text-red-600">{pinErrors[m.id]}</p>}
        </>
      )}
    </div>
  );
}
