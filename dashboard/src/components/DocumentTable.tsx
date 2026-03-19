import { useCallback } from "react";
import { useNavigate } from "react-router";
import type { DocumentSummaryResponse } from "../types/api";
import { StatusBadge } from "./StatusBadge";

interface DocumentTableProps {
  documents: DocumentSummaryResponse[];
}

function formatDate(iso: string): string {
  return new Date(iso).toLocaleDateString("ro-RO", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

export function DocumentTable({ documents }: DocumentTableProps) {
  const navigate = useNavigate();

  const handleRowClick = useCallback(
    (id: number) => {
      navigate(`/documents/${id}`);
    },
    [navigate],
  );

  if (documents.length === 0) {
    return (
      <div className="rounded-lg border border-gray-200 bg-white p-8 text-center text-sm text-gray-500">
        No documents found.
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
      <table className="min-w-full divide-y divide-gray-200">
        <thead className="bg-gray-50">
          <tr>
            <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">ID</th>
            <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">External Ref</th>
            <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Type</th>
            <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Format</th>
            <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Status</th>
            <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Created</th>
            <th className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500">Updated</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-200">
          {documents.map((doc) => (
            <tr
              key={doc.id}
              onClick={() => handleRowClick(doc.id)}
              className="cursor-pointer hover:bg-gray-50"
            >
              <td className="whitespace-nowrap px-4 py-3 text-sm font-medium text-gray-900">{doc.id}</td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">{doc.externalRef || "\u2014"}</td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">{doc.documentType || "\u2014"}</td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">{doc.inputFormat}</td>
              <td className="whitespace-nowrap px-4 py-3 text-sm">
                <StatusBadge status={doc.pipelineStatus} />
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">{formatDate(doc.createdAt)}</td>
              <td className="whitespace-nowrap px-4 py-3 text-sm text-gray-600">{formatDate(doc.updatedAt)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
