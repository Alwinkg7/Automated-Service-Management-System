"use client";
// Thin wrappers around recharts with our theme tokens. Imported via
// next/dynamic so recharts only loads on pages that show charts.
import {
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Line,
  LineChart,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";

// Categorical palette chosen to stay distinguishable in light and dark themes.
export const SERIES_COLORS = [
  "#2453d6",
  "#12a37f",
  "#e08a00",
  "#c6283b",
  "#7c4dff",
  "#0b8ec2",
  "#d94f9b",
  "#6b7a8f",
];

const axis = { stroke: "var(--fg-muted)", fontSize: 12, tickLine: false, axisLine: false } as const;
const tooltipStyle = {
  contentStyle: {
    background: "var(--surface)",
    border: "1px solid var(--border)",
    borderRadius: 8,
    color: "var(--fg)",
    fontSize: 12,
  },
  labelStyle: { color: "var(--fg-muted)" },
};

export interface SeriesDef {
  key: string;
  label: string;
  color?: string;
}

type Row = Record<string, string | number>;

export function LineSeriesChart({
  data,
  xKey,
  series,
  format,
}: {
  data: Row[];
  xKey: string;
  series: SeriesDef[];
  format?: (v: number) => string;
}) {
  return (
    <ResponsiveContainer width="100%" height={260}>
      <LineChart data={data} margin={{ top: 8, right: 8, bottom: 0, left: 0 }}>
        <CartesianGrid stroke="var(--border)" vertical={false} />
        <XAxis dataKey={xKey} {...axis} />
        <YAxis {...axis} width={56} tickFormatter={format} />
        <Tooltip {...tooltipStyle} formatter={(v) => (format ? format(Number(v)) : String(v))} />
        {series.length > 1 ? <Legend wrapperStyle={{ fontSize: 12 }} /> : null}
        {series.map((s, i) => (
          <Line
            key={s.key}
            type="monotone"
            dataKey={s.key}
            name={s.label}
            stroke={s.color ?? SERIES_COLORS[i]}
            strokeWidth={2}
            dot={false}
          />
        ))}
      </LineChart>
    </ResponsiveContainer>
  );
}

export function BarSeriesChart({
  data,
  xKey,
  series,
  format,
  horizontal = false,
}: {
  data: Row[];
  xKey: string;
  series: SeriesDef[];
  format?: (v: number) => string;
  horizontal?: boolean;
}) {
  return (
    <ResponsiveContainer width="100%" height={horizontal ? Math.max(200, data.length * 36) : 260}>
      <BarChart
        data={data}
        layout={horizontal ? "vertical" : "horizontal"}
        margin={{ top: 8, right: 8, bottom: 0, left: 0 }}
      >
        <CartesianGrid stroke="var(--border)" vertical={horizontal} horizontal={!horizontal} />
        {horizontal ? (
          <>
            <XAxis type="number" {...axis} tickFormatter={format} allowDecimals={false} />
            <YAxis type="category" dataKey={xKey} {...axis} width={110} />
          </>
        ) : (
          <>
            <XAxis dataKey={xKey} {...axis} />
            <YAxis {...axis} width={56} tickFormatter={format} allowDecimals={false} />
          </>
        )}
        <Tooltip
          {...tooltipStyle}
          cursor={{ fill: "var(--surface-muted)" }}
          formatter={(v) => (format ? format(Number(v)) : String(v))}
        />
        {series.map((s, i) => (
          <Bar
            key={s.key}
            dataKey={s.key}
            name={s.label}
            fill={s.color ?? SERIES_COLORS[i]}
            radius={[4, 4, 4, 4]}
            maxBarSize={36}
          />
        ))}
      </BarChart>
    </ResponsiveContainer>
  );
}

export function DonutChart({ data }: { data: { name: string; value: number }[] }) {
  const visible = data.filter((d) => d.value > 0);
  return (
    <ResponsiveContainer width="100%" height={260}>
      <PieChart>
        <Pie
          data={visible}
          dataKey="value"
          nameKey="name"
          innerRadius="55%"
          outerRadius="80%"
          paddingAngle={2}
          stroke="var(--surface)"
        >
          {visible.map((d, i) => (
            <Cell key={d.name} fill={SERIES_COLORS[i % SERIES_COLORS.length]} />
          ))}
        </Pie>
        <Tooltip {...tooltipStyle} />
        <Legend wrapperStyle={{ fontSize: 12 }} />
      </PieChart>
    </ResponsiveContainer>
  );
}
