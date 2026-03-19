import { useCallback, useEffect, useRef, useState } from "react";
import { StatsCard, StatsCardSkeleton } from "../components/StatsCard";
import type { DocumentStats } from "../services/api/stats";
import { getDocumentStats } from "../services/api/stats";

const REFRESH_INTERVAL_MS = 30_000;

interface StatCardConfig {
  key: string;
  title: string;
  icon: string;
  colorClass: string;
  bgClass: string;
  getValue: (stats: DocumentStats) => number;
}

const STAT_CARDS: StatCardConfig[] = [
  {
    key: "total",
    title: "Total Documents",
    icon: "\u{1F4C4}",
    colorClass: "text-indigo-600",
    bgClass: "bg-indigo-50",
    getValue: (s) => s.total,
  },
  {
    key: "completed",
    title: "Completed",
    icon: "\u2705",
    colorClass: "text-green-600",
    bgClass: "bg-green-50",
    getValue: (s) => s.byStatus["completed"] ?? 0,
  },
  {
    key: "failed",
    title: "Failed",
    icon: "\u274C",
    colorClass: "text-red-600",
    bgClass: "bg-red-50",
    getValue: (s) => s.byStatus["failed"] ?? 0,
  },
  {
    key: "review",
    title: "In Review",
    icon: "\u{1F50D}",
    colorClass: "text-amber-600",
    bgClass: "bg-amber-50",
    getValue: (s) => s.byStatus["review_required"] ?? 0,
  },
  {
    key: "processing",
    title: "Processing",
    icon: "\u23F3",
    colorClass: "text-blue-600",
    bgClass: "bg-blue-50",
    getValue: (s) =>
      (s.byStatus["triaging"] ?? 0) + (s.byStatus["extracting"] ?? 0),
  },
];

export function HomePage() {
  const [stats, setStats] = useState<DocumentStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const mountedRef = useRef(true);

  const fetchStats = useCallback(async (showLoading: boolean) => {
    if (showLoading) {
      setLoading(true);
    }
    setError(null);
    try {
      const data = await getDocumentStats();
      if (mountedRef.current) {
        setStats(data);
      }
    } catch (err) {
      if (mountedRef.current) {
        setError(err instanceof Error ? err.message : "Failed to load stats.");
      }
    } finally {
      if (mountedRef.current) {
        setLoading(false);
      }
    }
  }, []);

  useEffect(() => {
    mountedRef.current = true;
    fetchStats(true);

    const interval = setInterval(() => {
      fetchStats(false);
    }, REFRESH_INTERVAL_MS);

    return () => {
      mountedRef.current = false;
      clearInterval(interval);
    };
  }, [fetchStats]);

  if (loading && !stats) {
    return (
      <div>
        <h2 className="text-lg font-medium text-gray-900">Pipeline Overview</h2>
        <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
          {STAT_CARDS.map((card) => (
            <StatsCardSkeleton key={card.key} />
          ))}
        </div>
      </div>
    );
  }

  if (error && !stats) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-6">
        <p className="text-sm font-medium text-red-800">
          Failed to load dashboard statistics.
        </p>
        <p className="mt-1 text-sm text-red-600">{error}</p>
        <button
          type="button"
          onClick={() => fetchStats(true)}
          className="mt-3 rounded-md bg-red-100 px-3 py-1.5 text-sm font-medium text-red-700 hover:bg-red-200"
        >
          Retry
        </button>
      </div>
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-medium text-gray-900">Pipeline Overview</h2>
        {error && (
          <span className="text-xs text-red-500">Refresh failed</span>
        )}
      </div>
      <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
        {stats &&
          STAT_CARDS.map((card) => (
            <StatsCard
              key={card.key}
              title={card.title}
              value={card.getValue(stats)}
              icon={card.icon}
              colorClass={card.colorClass}
              bgClass={card.bgClass}
            />
          ))}
      </div>
    </div>
  );
}
