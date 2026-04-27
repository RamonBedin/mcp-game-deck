import { useEffect, useRef, useState } from "react";
import type { FormEvent, KeyboardEvent } from "react";
import { onMessageReceived } from "../ipc/events";
import type { MessageRole } from "../ipc/types";
import { useConversationStore } from "../stores/conversationStore";

const roleColor = (role: MessageRole): string => {
  switch (role) {
    case "user":
      return "text-sky-400";
    case "assistant":
      return "text-emerald-400";
    case "system":
      return "text-amber-400";
  }
};

export default function ChatRoute() {
  const messages = useConversationStore((s) => s.messages);
  const sendMessage = useConversationStore((s) => s.sendMessage);
  const appendMessage = useConversationStore((s) => s.appendMessage);

  const [input, setInput] = useState("");
  const bottomRef = useRef<HTMLDivElement>(null);

  // Subscribe once: assistant messages from the Node SDK arrive via the
  // `message-received` event (dispatched in jsonrpc.rs from the Node-side
  // `message/received` notification). 5.2 will make these actually fire.
  useEffect(() => {
    let cancelled = false;
    let unlisten: (() => void) | null = null;

    onMessageReceived((message) => {
      if (cancelled) return;
      appendMessage(message);
    })
      .then((u) => {
        if (cancelled) {
          u();
        } else {
          unlisten = u;
        }
      })
      .catch((err) => {
        console.error("[chat] failed to subscribe to message-received:", err);
      });

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, [appendMessage]);

  // Auto-anchor the scroll to the bottom on every new message.
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth", block: "end" });
  }, [messages.length]);

  const submit = () => {
    if (!input.trim()) return;
    void sendMessage(input);
    setInput("");
  };

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    submit();
  };

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    // Enter sends, Shift+Enter inserts a newline.
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      submit();
    }
  };

  return (
    <div className="flex h-full flex-col">
      <h1 className="mb-3 text-xs font-semibold uppercase tracking-wider text-slate-500">
        Chat
      </h1>

      <div className="flex-1 space-y-2 overflow-y-auto pr-1">
        {messages.length === 0 ? (
          <p className="text-sm text-slate-500">
            Type a message and press Enter to send.
          </p>
        ) : (
          messages.map((m) => (
            <div key={m.id} className="rounded bg-slate-800/40 p-3">
              <div
                className={`mb-1 text-xs font-semibold uppercase tracking-wide ${roleColor(m.role)}`}
              >
                {m.role}
              </div>
              <div className="whitespace-pre-wrap font-mono text-sm text-slate-200">
                {m.content}
              </div>
            </div>
          ))
        )}
        <div ref={bottomRef} />
      </div>

      <form onSubmit={handleSubmit} className="mt-3 flex flex-col gap-2">
        <textarea
          value={input}
          onChange={(e) => setInput(e.target.value)}
          onKeyDown={handleKeyDown}
          rows={3}
          placeholder="Type a message... (Enter to send, Shift+Enter for newline)"
          className="resize-none rounded border border-slate-700 bg-slate-800 px-3 py-2 font-mono text-sm text-slate-100 focus:border-slate-500 focus:outline-none"
        />
        <button
          type="submit"
          disabled={!input.trim()}
          className="self-end rounded bg-sky-700 px-4 py-1.5 text-sm text-sky-50 hover:bg-sky-600 disabled:cursor-not-allowed disabled:opacity-50"
        >
          Send
        </button>
      </form>
    </div>
  );
}