import type { ReviewFlagResponse } from "../types/api";

interface ReviewFlagsSectionProps {
  flags: ReviewFlagResponse[];
}

const SEVERITY_STYLES: Record<string, string> = {
  high: "border-red-300 bg-red-50 text-red-800",
  medium: "border-amber-300 bg-amber-50 text-amber-800",
  low: "border-yellow-200 bg-yellow-50 text-yellow-800",
};

function formatFlagType(type: string): string {
  return type
    .replace(/_/g, " ")
    .replace(/\b\w/g, (c) => c.toUpperCase());
}

export function ReviewFlagsSection({ flags }: ReviewFlagsSectionProps) {
  if (flags.length === 0) {
    return null;
  }

  return (
    <div className="space-y-2">
      {flags.map((flag) => {
        const style = SEVERITY_STYLES[flag.severity] ?? "border-gray-200 bg-gray-50 text-gray-700";
        return (
          <div key={flag.id} className={`rounded-lg border p-3 ${style}`}>
            <div className="flex items-start justify-between gap-2">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-semibold uppercase tracking-wide">
                    {formatFlagType(flag.flagType)}
                  </span>
                  <span className="rounded-full bg-white/60 px-1.5 py-0.5 text-[10px] font-medium uppercase">
                    {flag.severity}
                  </span>
                </div>
                <p className="mt-1 text-sm">{flag.message}</p>
              </div>
              {flag.isResolved && (
                <span className="shrink-0 rounded-full bg-green-100 px-2 py-0.5 text-[10px] font-medium text-green-700">
                  Resolved
                </span>
              )}
            </div>
          </div>
        );
      })}
    </div>
  );
}
