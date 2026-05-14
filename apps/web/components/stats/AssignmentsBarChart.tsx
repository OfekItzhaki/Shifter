"use client";

import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from "recharts";

interface AssignmentsBarChartProps {
  data: Array<{ name: string; total: number }>;
  timeWindow?: string;
}

export default function AssignmentsBarChart({
  data,
  timeWindow,
}: AssignmentsBarChartProps) {
  if (data.length === 0) {
    return (
      <div className="flex items-center justify-center h-[300px] text-sm text-slate-400">
        אין נתונים להצגה
      </div>
    );
  }

  return (
    <div className="w-full" dir="ltr">
      {timeWindow && (
        <p className="text-xs text-slate-500 mb-2 text-right" dir="rtl">
          סה&quot;כ שיבוצים לאדם ({timeWindow})
        </p>
      )}
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
          <Bar
            dataKey="total"
            fill="#3b82f6"
            radius={[4, 4, 0, 0]}
            name="שיבוצים"
          />
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
