"use client";

import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
  Legend,
} from "recharts";

const PERSON_COLORS = [
  "#3b82f6", // blue
  "#ef4444", // red
  "#10b981", // emerald
  "#f59e0b", // amber
  "#8b5cf6", // violet
  "#ec4899", // pink
  "#06b6d4", // cyan
  "#f97316", // orange
  "#14b8a6", // teal
  "#6366f1", // indigo
];

interface BurdenTrendChartProps {
  data: Array<{ date: string; [personName: string]: number | string }>;
  people: string[];
}

export default function BurdenTrendChart({
  data,
  people,
}: BurdenTrendChartProps) {
  if (data.length === 0) {
    return (
      <div className="flex items-center justify-center h-[300px] text-sm text-slate-400">
        אין נתונים להצגה
      </div>
    );
  }

  return (
    <div className="w-full" dir="ltr">
      <p className="text-xs text-slate-500 mb-2 text-right" dir="rtl">
        מגמת ציון עומס לאורך זמן
      </p>
      <ResponsiveContainer width="100%" height={300}>
        <LineChart data={data} margin={{ top: 10, right: 20, left: 10, bottom: 5 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
          <XAxis
            dataKey="date"
            tick={{ fontSize: 11, fill: "#64748b" }}
            tickLine={false}
            axisLine={{ stroke: "#e2e8f0" }}
          />
          <YAxis
            tick={{ fontSize: 12, fill: "#64748b" }}
            tickLine={false}
            axisLine={false}
            allowDecimals={false}
          />
          <Tooltip
            contentStyle={{
              borderRadius: "8px",
              border: "1px solid #e2e8f0",
              fontSize: "12px",
            }}
            labelStyle={{ fontWeight: 600 }}
          />
          <Legend
            wrapperStyle={{ fontSize: "11px", paddingTop: "8px" }}
          />
          {people.map((person, idx) => (
            <Line
              key={person}
              type="monotone"
              dataKey={person}
              stroke={PERSON_COLORS[idx % PERSON_COLORS.length]}
              strokeWidth={2}
              dot={false}
              activeDot={{ r: 4 }}
              name={person}
            />
          ))}
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
