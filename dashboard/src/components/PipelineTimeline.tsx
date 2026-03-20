import { useMemo } from "react";
import type { DocumentEventResponse } from "../types/api";

interface PipelineTimelineProps {
  events: DocumentEventResponse[];
}

const EVENT_TYPE_COLORS: Record<string, string> = {
  status_change: "bg-blue-500",
  ingestion: "bg-green-500",
  triage: "bg-indigo-500",
  extraction: "bg-purple-500",
  review: "bg-amber-500",
  retry: "bg-orange-500",
  error: "bg-red-500",
};

function formatTimestamp(iso: string): string {
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "-";
  return d.toLocaleString("ro-RO", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

function formatStatusLabel(status: string): string {
  return status
    .replace(/_/g, " ")
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

export function PipelineTimeline({ events }: PipelineTimelineProps) {
  const sorted = useMemo(
    () => [...events].sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()),
    [events],
  );

  if (sorted.length === 0) {
    return (
      <div className="rounded-lg border border-gray-200 bg-gray-50 p-6 text-center">
        <p className="text-sm text-gray-500">No pipeline events yet.</p>
      </div>
    );
  }

  return (
    <div className="relative space-y-0">
      {sorted.map((event, idx) => {
        const dotColor = EVENT_TYPE_COLORS[event.eventType] ?? "bg-gray-400";
        const isLast = idx === sorted.length - 1;
        return (
          <div key={event.id} className="relative flex gap-3 pb-4">
            <div className="flex flex-col items-center">
              <div className={`mt-1 h-2.5 w-2.5 shrink-0 rounded-full ${dotColor}`} />
              {!isLast && <div className="w-px grow bg-gray-200" />}
            </div>
            <div className="min-w-0 pb-2">
              <div className="flex flex-wrap items-baseline gap-2">
                <span className="text-sm font-medium text-gray-800">
                  {formatStatusLabel(event.eventType)}
                </span>
                {event.fromStatus && event.toStatus && (
                  <span className="text-xs text-gray-500">
                    {formatStatusLabel(event.fromStatus)} → {formatStatusLabel(event.toStatus)}
                  </span>
                )}
              </div>
              {event.details && (
                <p className="mt-0.5 text-xs text-gray-600">{event.details}</p>
              )}
              <p className="mt-0.5 text-xs text-gray-400">{formatTimestamp(event.createdAt)}</p>
            </div>
          </div>
        );
      })}
    </div>
  );
}
