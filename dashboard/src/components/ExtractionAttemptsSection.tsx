import { useCallback, useState } from "react";
import type { ExtractionAttemptResponse } from "../types/api";

interface ExtractionAttemptsSectionProps {
  attempts: ExtractionAttemptResponse[];
}

const STATUS_STYLES: Record<string, string> = {
  completed: "bg-green-100 text-green-700",
  failed: "bg-red-100 text-red-700",
  running: "bg-blue-100 text-blue-700",
  pending: "bg-gray-100 text-gray-700",
};

function formatTimestamp(iso: string | null): string {
  if (!iso) return "-";
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

export function ExtractionAttemptsSection({ attempts }: ExtractionAttemptsSectionProps) {
  const [expanded, setExpanded] = useState(false);

  const handleToggle = useCallback(() => {
    setExpanded((prev) => !prev);
  }, []);

  if (attempts.length === 0) {
    return null;
  }

  return (
    <div>
      <button
        onClick={handleToggle}
        className="flex w-full items-center justify-between rounded-lg border border-gray-200 bg-white px-4 py-2.5 text-left text-sm font-medium text-gray-700 hover:bg-gray-50"
      >
        <span>Extraction Attempts ({attempts.length})</span>
        <svg
          className={`h-4 w-4 shrink-0 text-gray-400 transition-transform ${expanded ? "rotate-180" : ""}`}
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 9l-7 7-7-7" />
        </svg>
      </button>
      {expanded && (
        <div className="mt-2 space-y-2">
          {attempts.map((attempt) => {
            const statusStyle = STATUS_STYLES[attempt.status] ?? "bg-gray-100 text-gray-700";
            return (
              <div key={attempt.id} className="rounded-lg border border-gray-200 bg-white p-3">
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-gray-800">
                      Attempt #{attempt.attemptNumber}
                    </span>
                    <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${statusStyle}`}>
                      {attempt.status}
                    </span>
                  </div>
                  {attempt.confidence != null && (
                    <span className="text-xs text-gray-500">
                      Confidence: {(attempt.confidence * 100).toFixed(1)}%
                    </span>
                  )}
                </div>
                <div className="mt-2 grid grid-cols-2 gap-x-4 gap-y-1 text-xs text-gray-600 sm:grid-cols-4">
                  <div>
                    <span className="font-medium text-gray-500">Phase:</span> {attempt.phase}
                  </div>
                  <div>
                    <span className="font-medium text-gray-500">Model:</span> {attempt.modelId}
                  </div>
                  {attempt.inputTokens != null && (
                    <div>
                      <span className="font-medium text-gray-500">In tokens:</span> {attempt.inputTokens.toLocaleString("ro-RO")}
                    </div>
                  )}
                  {attempt.outputTokens != null && (
                    <div>
                      <span className="font-medium text-gray-500">Out tokens:</span> {attempt.outputTokens.toLocaleString("ro-RO")}
                    </div>
                  )}
                  {attempt.latencyMs != null && (
                    <div>
                      <span className="font-medium text-gray-500">Latency:</span> {attempt.latencyMs.toLocaleString("ro-RO")}ms
                    </div>
                  )}
                  <div>
                    <span className="font-medium text-gray-500">Prompt:</span> {attempt.promptVersion}
                  </div>
                  <div>
                    <span className="font-medium text-gray-500">Started:</span> {formatTimestamp(attempt.createdAt)}
                  </div>
                  <div>
                    <span className="font-medium text-gray-500">Completed:</span> {formatTimestamp(attempt.completedAt)}
                  </div>
                </div>
                {attempt.errorMessage && (
                  <div className="mt-2 rounded border border-red-200 bg-red-50 px-2 py-1.5 text-xs text-red-700">
                    {attempt.errorMessage}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
