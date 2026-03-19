import { useCallback, useState } from "react";

interface StatusFilterProps {
  selectedStatuses: string[];
  onChange: (statuses: string[]) => void;
}

const ALL_STATUSES = [
  { value: "ingested", label: "Ingested" },
  { value: "pending_triage", label: "Pending Triage" },
  { value: "triaging", label: "Triaging" },
  { value: "pending_extraction", label: "Pending Extraction" },
  { value: "extracting", label: "Extracting" },
  { value: "completed", label: "Completed" },
  { value: "extraction_failed", label: "Extraction Failed" },
  { value: "failed", label: "Failed" },
  { value: "review_required", label: "Review Required" },
  { value: "rejected", label: "Rejected" },
] as const;

export function StatusFilter({ selectedStatuses, onChange }: StatusFilterProps) {
  const [open, setOpen] = useState(false);

  const toggle = useCallback(() => {
    setOpen((prev) => !prev);
  }, []);

  const handleToggleStatus = useCallback(
    (status: string) => {
      const next = selectedStatuses.includes(status)
        ? selectedStatuses.filter((s) => s !== status)
        : [...selectedStatuses, status];
      onChange(next);
    },
    [selectedStatuses, onChange],
  );

  const handleClear = useCallback(() => {
    onChange([]);
  }, [onChange]);

  return (
    <div className="relative">
      <button
        type="button"
        onClick={toggle}
        className="flex items-center gap-1 rounded-md border border-gray-300 bg-white px-3 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50"
      >
        Status
        {selectedStatuses.length > 0 && (
          <span className="ml-1 rounded-full bg-blue-100 px-1.5 py-0.5 text-xs font-medium text-blue-700">
            {selectedStatuses.length}
          </span>
        )}
        <svg className={`ml-1 h-4 w-4 transition-transform ${open ? "rotate-180" : ""}`} fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
        </svg>
      </button>
      {open && (
        <div className="absolute left-0 z-10 mt-1 w-56 rounded-md border border-gray-200 bg-white py-1 shadow-lg">
          {ALL_STATUSES.map((s) => (
            <label
              key={s.value}
              className="flex cursor-pointer items-center gap-2 px-3 py-1.5 text-sm text-gray-700 hover:bg-gray-50"
            >
              <input
                type="checkbox"
                checked={selectedStatuses.includes(s.value)}
                onChange={() => handleToggleStatus(s.value)}
                className="rounded border-gray-300"
              />
              {s.label}
            </label>
          ))}
          {selectedStatuses.length > 0 && (
            <div className="border-t border-gray-100 px-3 py-1.5">
              <button
                type="button"
                onClick={handleClear}
                className="text-xs text-blue-600 hover:text-blue-800"
              >
                Clear all
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
