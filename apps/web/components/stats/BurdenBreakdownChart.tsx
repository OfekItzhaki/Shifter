"use client";

import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
  Legend,
} from "recharts";

interface BurdenBreakdownChartProps {
  data: Array<{ name: string; hard: number; normal: number; easy: number }>;
}

export default function BurdenBreakdownChart({
  data,
}: BurdenBreakdownChartProps) {
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
        פילוח לפי רמת קושי
      </p>
      <ResponsiveContainer width="100%" height={300}>
        <BarChart data={data} margin={{ top: 10, right: 20, left: 10, bottom: 5 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
          <XAxis
            dataKey="name"
            tick={{ fontSize: 12, fill: "#64748b" }}
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
            wrapperStyle={{ fontSize: "12px", paddingTop: "8px" }}
          />
          <Bar
            dataKey="hard"
            stackId="burden"
            fill="#dc2626"
            name="קשה"
            radius={[0, 0, 0, 0]}
          />
          <Bar
            dataKey="normal"
            stackId="burden"
            fill="#6b7280"
            name="רגיל"
            radius={[0, 0, 0, 0]}
          />
          <Bar
            dataKey="easy"
            stackId="burden"
            fill="#16a34a"
            name="קל"
            radius={[4, 4, 0, 0]}
          />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
