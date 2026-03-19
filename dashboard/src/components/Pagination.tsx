import { useCallback } from "react";

interface PaginationProps {
  page: number;
  pageSize: number;
  totalRecords: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}

const PAGE_SIZE_OPTIONS = [10, 25, 50] as const;

export function Pagination({ page, pageSize, totalRecords, onPageChange, onPageSizeChange }: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(totalRecords / pageSize));
  const from = totalRecords === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalRecords);

  const handlePrev = useCallback(() => {
    if (page > 1) onPageChange(page - 1);
  }, [page, onPageChange]);

  const handleNext = useCallback(() => {
    if (page < totalPages) onPageChange(page + 1);
  }, [page, totalPages, onPageChange]);

  const handlePageSizeChange = useCallback(
    (e: React.ChangeEvent<HTMLSelectElement>) => {
      onPageSizeChange(Number(e.target.value));
    },
    [onPageSizeChange],
  );

  return (
    <div className="flex items-center justify-between border-t border-gray-200 px-2 py-3">
      <div className="flex items-center gap-2 text-sm text-gray-600">
        <span>Rows per page:</span>
        <select
          value={pageSize}
          onChange={handlePageSizeChange}
          className="rounded border border-gray-300 bg-white px-2 py-1 text-sm"
        >
          {PAGE_SIZE_OPTIONS.map((size) => (
            <option key={size} value={size}>
              {size}
            </option>
          ))}
        </select>
      </div>
      <div className="flex items-center gap-3 text-sm text-gray-600">
        <span>
          {from}–{to} of {totalRecords}
        </span>
        <div className="flex gap-1">
          <button
            type="button"
            onClick={handlePrev}
            disabled={page <= 1}
            className="rounded border border-gray-300 px-2 py-1 text-sm disabled:cursor-not-allowed disabled:opacity-40"
          >
            Prev
          </button>
          <span className="flex items-center px-2 text-sm font-medium">
            {page} / {totalPages}
          </span>
          <button
            type="button"
            onClick={handleNext}
            disabled={page >= totalPages}
            className="rounded border border-gray-300 px-2 py-1 text-sm disabled:cursor-not-allowed disabled:opacity-40"
          >
            Next
          </button>
        </div>
      </div>
    </div>
  );
}
