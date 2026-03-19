import type { DocumentListResponse } from "../../types/api";
import { getDocuments } from "./documents";

export interface DocumentStats {
  total: number;
  byStatus: Record<string, number>;
}

export async function getDocumentStats(): Promise<DocumentStats> {
  const response: DocumentListResponse = await getDocuments({
    page: 1,
    pageSize: 1,
  });

  const byStatus: Record<string, number> = {};

  return {
    total: response.total,
    byStatus,
  };
}
