import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { queryKeys } from '@/lib/query-client';
import type { CreateDocumentRequest, StockDocumentDto, StockDocumentsQuery } from '@/types';
import { documentsApi } from '../api/documents-api';
import type { DocumentTypeConfig } from '../config';

export function useDocuments(cfg: DocumentTypeConfig, query: StockDocumentsQuery) {
  return useQuery({
    queryKey: queryKeys.documentList(cfg.type, query),
    queryFn: () => documentsApi.list(cfg, query),
    placeholderData: keepPreviousData,
  });
}

export function useDocument(cfg: DocumentTypeConfig, id: number | undefined) {
  return useQuery({
    queryKey: queryKeys.document(cfg.type, id ?? 0),
    queryFn: () => documentsApi.get(cfg, id!),
    enabled: !!id,
  });
}

/** Posting changes stock → refresh the stock table and "available" lookups too. */
function useOnDocumentChanged(cfg: DocumentTypeConfig) {
  const queryClient = useQueryClient();
  return (doc: StockDocumentDto) => {
    queryClient.setQueryData(queryKeys.document(cfg.type, doc.id), doc);
    void queryClient.invalidateQueries({ queryKey: queryKeys.documents(cfg.type) });
    if (doc.status === 'Posted') void queryClient.invalidateQueries({ queryKey: queryKeys.stock });
  };
}

export function useCreateDocument(cfg: DocumentTypeConfig) {
  const onChanged = useOnDocumentChanged(cfg);
  return useMutation({
    mutationFn: ({ body, idempotencyKey }: { body: CreateDocumentRequest; idempotencyKey: string }) =>
      documentsApi.create(cfg, body, idempotencyKey),
    // the form maps 400 field errors / 409 shortages onto its lines
    meta: { suppressGlobalError: true },
    onSuccess: onChanged,
  });
}

export function usePostDocument(cfg: DocumentTypeConfig) {
  const onChanged = useOnDocumentChanged(cfg);
  return useMutation({
    mutationFn: (doc: Pick<StockDocumentDto, 'id' | 'rowVersion'>) => documentsApi.post(cfg, doc.id, doc.rowVersion),
    meta: { suppressGlobalError: true },
    onSuccess: onChanged,
  });
}

export function useCancelDocument(cfg: DocumentTypeConfig) {
  const onChanged = useOnDocumentChanged(cfg);
  return useMutation({
    mutationFn: (doc: Pick<StockDocumentDto, 'id' | 'rowVersion'>) => documentsApi.cancel(cfg, doc.id, doc.rowVersion),
    onSuccess: onChanged,
  });
}
