"use client";

import { LeaderboardEntry } from "@/lib/api/schedule";

interface Props {
  title: string;
  entries: LeaderboardEntry[];
  valueColor?: string;
}

const card: React.CSSProperties = {
  background: "white",
  borderRadius: 14,
  border: "1px solid #e2e8f0",
  boxShadow: "0 1px 4px rgba(0,0,0,0.05)",
  padding: "1.25rem",
};

export default function StatsLeaderboard({ title, entries, valueColor = "#0f172a" }: Props) {
  return (
    <div style={card}>
      <h3 style={{ fontSize: "0.875rem", fontWeight: 700, color: "#0f172a", margin: "0 0 0.875rem" }}>
        {title}
      </h3>
      {entries.length === 0 ? (
        <p style={{ fontSize: "0.8125rem", color: "#94a3b8", margin: 0 }}>אין נתונים</p>
      ) : (
        <ol style={{ margin: 0, padding: 0, listStyle: "none", display: "flex", flexDirection: "column", gap: 6 }}>
          {entries.map((e, i) => (
            <li key={e.personId} style={{ display: "flex", alignItems: "center", gap: 10 }}>
              <span style={{
                width: 22, height: 22, borderRadius: "50%", flexShrink: 0,
                background: i === 0 ? "#fbbf24" : i === 1 ? "#94a3b8" : i === 2 ? "#d97706" : "#e2e8f0",
                display: "flex", alignItems: "center", justifyContent: "center",
                fontSize: 11, fontWeight: 700, color: i < 3 ? "white" : "#64748b",
              }}>
                {i + 1}
              </span>
              {e.profileImageUrl ? (
                <img src={e.profileImageUrl} alt={e.displayName}
                  style={{ width: 28, height: 28, borderRadius: "50%", objectFit: "cover", flexShrink: 0 }} />
              ) : (
                <div style={{
                  width: 28, height: 28, borderRadius: "50%", background: "#e0e7ff",
                  display: "flex", alignItems: "center", justifyContent: "center",
                  fontSize: 11, fontWeight: 700, color: "#6366f1", flexShrink: 0,
                }}>
                  {e.displayName.charAt(0).toUpperCase()}
                </div>
              )}
              <span style={{ flex: 1, fontSize: "0.8125rem", color: "#374151", fontWeight: 500 }}>
                {e.displayName}
              </span>
              <span style={{ fontSize: "0.875rem", fontWeight: 700, color: valueColor }}>
                {e.value}
              </span>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
