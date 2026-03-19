import type { DocumentListResponse } from "../../types/api";
import { getDocuments } from "./documents";

export interface DocumentStats {
  total: number;
  byStatus: Record<string, number>;
}

export async function getDocumentStats(): Promise<DocumentStats> {
  const firstPage: DocumentListResponse = await getDocuments({
    page: 1,
    pageSize: 200,
  });

  const allItems = [...firstPage.items];
  const totalPages = Math.ceil(firstPage.total / 200);

  const remainingPages = [];
  for (let page = 2; page <= totalPages; page++) {
    remainingPages.push(getDocuments({ page, pageSize: 200 }));
  }

  const results = await Promise.all(remainingPages);
  for (const result of results) {
    allItems.push(...result.items);
  }

  const byStatus: Record<string, number> = {};
  for (const item of allItems) {
    byStatus[item.status] = (byStatus[item.status] ?? 0) + 1;
  }

  return {
    total: firstPage.total,
    byStatus,
  };
}
