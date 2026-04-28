"use client";

import { useState } from "react";
import { PersonBurdenStats } from "@/lib/api/schedule";

type SortKey = keyof Pick<PersonBurdenStats,
  "displayName" | "totalAssignmentsAllTime" | "hatedTasksAllTime" |
  "dislikedTasksAllTime" | "favorableTasksAllTime" | "burdenScoreAllTime" |
  "burdenBalance" | "lastAssignmentDate">;

interface Props { people: PersonBurdenStats[]; }

function formatDate(d: string | null): string {
  if (!d) return "—";
  try { return new Date(d).toLocaleDateString("he-IL", { day: "numeric", month: "short", year: "numeric" }); }
  catch { return d; }
}

const th: React.CSSProperties = {
  padding: "10px 12px", fontSize: "0.75rem", fontWeight: 700,
  color: "#64748b", textAlign: "right", whiteSpace: "nowrap",
  borderBottom: "2px solid #e2e8f0", cursor: "pointer", userSelect: "none",
};
const td: React.CSSProperties = {
  padding: "10px 12px", fontSize: "0.8125rem", color: "#374151",
  borderBottom: "1px solid #f1f5f9", verticalAlign: "middle",
};

export default function StatsPeopleTable({ people }: Props) {
  const [sortKey, setSortKey] = useState<SortKey>("burdenScoreAllTime");
  const [sortAsc, setSortAsc] = useState(false);

  function handleSort(key: SortKey) {
    if (sortKey === key) setSortAsc(a => !a);
    else { setSortKey(key); setSortAsc(false); }
  }

  const sorted = [...people].sort((a, b) => {
    const av = a[sortKey] ?? "";
    const bv = b[sortKey] ?? "";
    const cmp = av < bv ? -1 : av > bv ? 1 : 0;
    return sortAsc ? cmp : -cmp;
  });

  function SortIcon({ k }: { k: SortKey }) {
    if (sortKey !== k) return <span style={{ opacity: 0.3 }}> ↕</span>;
    return <span>{sortAsc ? " ↑" : " ↓"}</span>;
  }

  return (
    <div style={{ overflowX: "auto", borderRadius: 14, border: "1px solid #e2e8f0", background: "white" }}>
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead>
          <tr style={{ background: "#f8fafc" }}>
            {([
              ["displayName", "שם"],
              ["totalAssignmentsAllTime", "סה״כ"],
              ["hatedTasksAllTime", "שנואות"],
              ["dislikedTasksAllTime", "לא אהובות"],
              ["favorableTasksAllTime", "מועדפות"],
              ["burdenScoreAllTime", "ציון עומס"],
              ["burdenBalance", "איזון"],
              ["lastAssignmentDate", "שיבוץ אחרון"],
            ] as [SortKey, string][]).map(([k, label]) => (
              <th key={k} style={th} onClick={() => handleSort(k)}>
                {label}<SortIcon k={k} />
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {sorted.map(p => (
            <tr key={p.personId} style={{ transition: "background 0.1s" }}
              onMouseEnter={e => (e.currentTarget.style.background = "#f8fafc")}
              onMouseLeave={e => (e.currentTarget.style.background = "")}>
              <td style={{ ...td, fontWeight: 600, color: "#0f172a" }}>
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  {p.profileImageUrl ? (
                    <img src={p.profileImageUrl} alt={p.displayName}
                      style={{ width: 28, height: 28, borderRadius: "50%", objectFit: "cover" }} />
                  ) : (
                    <div style={{
                      width: 28, height: 28, borderRadius: "50%", background: "#e0e7ff",
                      display: "flex", alignItems: "center", justifyContent: "center",
                      fontSize: 11, fontWeight: 700, color: "#6366f1",
                    }}>
                      {p.displayName.charAt(0).toUpperCase()}
                    </div>
                  )}
                  {p.displayName}
                </div>
              </td>
              <td style={td}>{p.totalAssignmentsAllTime}</td>
              <td style={{ ...td, color: p.hatedTasksAllTime > 0 ? "#dc2626" : "#374151", fontWeight: p.hatedTasksAllTime > 0 ? 700 : 400 }}>
                {p.hatedTasksAllTime}
              </td>
              <td style={td}>{p.dislikedTasksAllTime}</td>
              <td style={{ ...td, color: p.favorableTasksAllTime > 0 ? "#16a34a" : "#374151", fontWeight: p.favorableTasksAllTime > 0 ? 700 : 400 }}>
                {p.favorableTasksAllTime}
              </td>
              <td style={{ ...td, fontWeight: 600 }}>{p.burdenScoreAllTime}</td>
              <td style={{
                ...td, fontWeight: 700,
                color: p.burdenBalance > 0 ? "#16a34a" : p.burdenBalance < 0 ? "#d97706" : "#374151",
              }}>
                {p.burdenBalance > 0 ? `+${p.burdenBalance}` : p.burdenBalance}
              </td>
              <td style={{ ...td, color: "#64748b", fontSize: "0.75rem" }}>{formatDate(p.lastAssignmentDate)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
