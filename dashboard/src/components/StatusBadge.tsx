interface StatusBadgeProps {
  status: string;
}

const STATUS_STYLES: Record<string, string> = {
  ingested: "bg-gray-100 text-gray-700",
  pending_triage: "bg-gray-100 text-gray-700",
  triaging: "bg-blue-100 text-blue-700",
  pending_extraction: "bg-blue-100 text-blue-700",
  extracting: "bg-indigo-100 text-indigo-700",
  completed: "bg-green-100 text-green-700",
  extraction_failed: "bg-red-100 text-red-700",
  failed: "bg-red-100 text-red-700",
  review_required: "bg-amber-100 text-amber-700",
  rejected: "bg-slate-100 text-slate-700",
};

const STATUS_LABELS: Record<string, string> = {
  ingested: "Ingested",
  pending_triage: "Pending Triage",
  triaging: "Triaging",
  pending_extraction: "Pending Extraction",
  extracting: "Extracting",
  completed: "Completed",
  extraction_failed: "Extraction Failed",
  failed: "Failed",
  review_required: "Review Required",
  rejected: "Rejected",
};

export function StatusBadge({ status }: StatusBadgeProps) {
  const style = STATUS_STYLES[status] ?? "bg-gray-100 text-gray-700";
  const label = STATUS_LABELS[status] ?? status;
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${style}`}>
      {label}
    </span>
  );
}
