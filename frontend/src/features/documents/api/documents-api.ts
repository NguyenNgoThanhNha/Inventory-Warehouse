import { api, cleanParams } from '@/lib/api-client';
import type { CreateDocumentRequest, PagedResult, StockDocumentDto, StockDocumentListItemDto, StockDocumentsQuery } from '@/types';
import type { DocumentTypeConfig } from '../config';

export const documentsApi = {
  list: (cfg: DocumentTypeConfig, query: StockDocumentsQuery) =>
    api.get<PagedResult<StockDocumentListItemDto>>(`/${cfg.path}`, { params: cleanParams(query) }).then((r) => r.data),
  get: (cfg: DocumentTypeConfig, id: number) => api.get<StockDocumentDto>(`/${cfg.path}/${id}`).then((r) => r.data),
  /** The same Idempotency-Key on a retry returns the document created by the first attempt. */
  create: (cfg: DocumentTypeConfig, body: CreateDocumentRequest, idempotencyKey: string) =>
    api
      .post<StockDocumentDto>(`/${cfg.path}`, body, { headers: { 'Idempotency-Key': idempotencyKey } })
      .then((r) => r.data),
  post: (cfg: DocumentTypeConfig, id: number, rowVersion: string) =>
    api.post<StockDocumentDto>(`/${cfg.path}/${id}/post`, { rowVersion }).then((r) => r.data),
  cancel: (cfg: DocumentTypeConfig, id: number, rowVersion: string) =>
    api.post<StockDocumentDto>(`/${cfg.path}/${id}/cancel`, { rowVersion }).then((r) => r.data),
};
