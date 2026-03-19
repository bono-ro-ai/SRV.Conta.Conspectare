interface StatsCardProps {
  title: string;
  value: number;
  icon: string;
  colorClass: string;
  bgClass: string;
}

export function StatsCard({ title, value, icon, colorClass, bgClass }: StatsCardProps) {
  return (
    <div className="rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
      <div className="flex items-center gap-3">
        <div className={`flex h-10 w-10 items-center justify-center rounded-lg ${bgClass}`}>
          <span className="text-lg">{icon}</span>
        </div>
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-gray-500">{title}</p>
          <p className={`text-2xl font-semibold ${colorClass}`}>
            {value.toLocaleString("ro-RO")}
          </p>
        </div>
      </div>
    </div>
  );
}

export function StatsCardSkeleton() {
  return (
    <div className="animate-pulse rounded-lg border border-gray-200 bg-white p-5 shadow-sm">
      <div className="flex items-center gap-3">
        <div className="h-10 w-10 rounded-lg bg-gray-200" />
        <div className="min-w-0 flex-1">
          <div className="h-4 w-24 rounded bg-gray-200" />
          <div className="mt-2 h-7 w-16 rounded bg-gray-200" />
        </div>
      </div>
    </div>
  );
}
