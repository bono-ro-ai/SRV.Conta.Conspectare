import { useCallback, useEffect, useRef, useState } from "react";
import { DocumentTable } from "../components/DocumentTable";
import { Pagination } from "../components/Pagination";
import { SearchInput } from "../components/SearchInput";
import { StatusFilter } from "../components/StatusFilter";
import { getDocuments } from "../services/api/documents";
import type { DocumentSummaryResponse } from "../types/api";

export function DocumentsPage() {
  const [documents, setDocuments] = useState<DocumentSummaryResponse[]>([]);
  const [totalRecords, setTotalRecords] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedStatuses, setSelectedStatuses] = useState<string[]>([]);
  const [search, setSearch] = useState("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);
  const mountedRef = useRef(true);

  useEffect(() => {
    mountedRef.current = true;
    let cancelled = false;
    setLoading(true);
    setError(null);
    getDocuments({
      status: selectedStatuses.length > 0 ? selectedStatuses.join(",") : undefined,
      search: search || undefined,
      dateFrom: dateFrom || undefined,
      dateTo: dateTo || undefined,
      page,
      pageSize,
    })
      .then((data) => {
        if (!cancelled && mountedRef.current) {
          setDocuments(data.items);
          setTotalRecords(data.total);
        }
      })
      .catch((err) => {
        if (!cancelled && mountedRef.current) {
          setError(err instanceof Error ? err.message : "Failed to load documents.");
        }
      })
      .finally(() => {
        if (!cancelled && mountedRef.current) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, [selectedStatuses, search, dateFrom, dateTo, page, pageSize]);

  useEffect(() => {
    return () => {
      mountedRef.current = false;
    };
  }, []);

  const handleStatusChange = useCallback((statuses: string[]) => {
    setSelectedStatuses(statuses);
    setPage(1);
  }, []);

  const handleSearchChange = useCallback((value: string) => {
    setSearch(value);
    setPage(1);
  }, []);

  const handleDateFromChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setDateFrom(e.target.value);
    setPage(1);
  }, []);

  const handleDateToChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setDateTo(e.target.value);
    setPage(1);
  }, []);

  const handlePageSizeChange = useCallback((size: number) => {
    setPageSize(size);
    setPage(1);
  }, []);

  return (
    <div>
      <h2 className="text-lg font-medium text-gray-900">Documents</h2>
      <div className="mt-4 flex flex-wrap items-center gap-3">
        <SearchInput value={search} onChange={handleSearchChange} />
        <StatusFilter selectedStatuses={selectedStatuses} onChange={handleStatusChange} />
        <div className="flex items-center gap-2">
          <label className="text-sm text-gray-600">From:</label>
          <input
            type="date"
            value={dateFrom}
            onChange={handleDateFromChange}
            className="rounded-md border border-gray-300 bg-white px-2 py-1.5 text-sm"
          />
        </div>
        <div className="flex items-center gap-2">
          <label className="text-sm text-gray-600">To:</label>
          <input
            type="date"
            value={dateTo}
            onChange={handleDateToChange}
            className="rounded-md border border-gray-300 bg-white px-2 py-1.5 text-sm"
          />
        </div>
      </div>
      <div className="mt-4">
        {loading && documents.length === 0 ? (
          <div className="flex items-center justify-center rounded-lg border border-gray-200 bg-white p-12">
            <span className="text-sm text-gray-500">Loading documents...</span>
          </div>
        ) : error && documents.length === 0 ? (
          <div className="rounded-lg border border-red-200 bg-red-50 p-6">
            <p className="text-sm font-medium text-red-800">Failed to load documents.</p>
            <p className="mt-1 text-sm text-red-600">{error}</p>
          </div>
        ) : (
          <>
            <DocumentTable documents={documents} />
            <Pagination
              page={page}
              pageSize={pageSize}
              totalRecords={totalRecords}
              onPageChange={setPage}
              onPageSizeChange={handlePageSizeChange}
            />
          </>
        )}
      </div>
    </div>
  );
}
