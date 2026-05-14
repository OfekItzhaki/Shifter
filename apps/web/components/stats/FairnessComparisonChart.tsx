"use client";

import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
  Cell,
  ReferenceLine,
} from "recharts";

interface FairnessComparisonChartProps {
  data: Array<{ name: string; deviation: number }>;
}

export default function FairnessComparisonChart({
  data,
}: FairnessComparisonChartProps) {
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
        סטייה מממוצע העומס הקבוצתי
      </p>
      <ResponsiveContainer width="100%" height={300}>
        <BarChart
          data={data}
          layout="vertical"
          margin={{ top: 10, right: 20, left: 60, bottom: 5 }}
        >
          <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" horizontal={false} />
          <XAxis
            type="number"
            tick={{ fontSize: 12, fill: "#64748b" }}
            tickLine={false}
            axisLine={{ stroke: "#e2e8f0" }}
          />
          <YAxis
            type="category"
            dataKey="name"
            tick={{ fontSize: 12, fill: "#64748b" }}
            tickLine={false}
            axisLine={false}
            width={55}
          />
          <Tooltip
            contentStyle={{
              borderRadius: "8px",
              border: "1px solid #e2e8f0",
              fontSize: "12px",
            }}
            formatter={(value: number) => [
              value > 0 ? `+${value.toFixed(1)}` : value.toFixed(1),
              "סטייה",
            ]}
          />
          <ReferenceLine x={0} stroke="#94a3b8" strokeDasharray="3 3" />
          <Bar dataKey="deviation" name="סטייה מהממוצע" radius={[0, 4, 4, 0]}>
            {data.map((entry, index) => (
              <Cell
                key={`cell-${index}`}
                fill={entry.deviation >= 0 ? "#dc2626" : "#16a34a"}
              />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  );
}
