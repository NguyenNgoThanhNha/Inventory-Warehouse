import { Link, useParams } from 'react-router-dom';
import { FileLock2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState } from '@/components/common/empty-state';
import type { DocumentType } from '@/types';
import { DOCUMENT_CONFIG, STATUS_LABEL } from '../config';
import { useDocument } from '../hooks/use-documents';
import { DocumentFormPage } from './document-form-page';

/** Loads a draft and opens the shared form in edit mode; posted / cancelled documents are read-only. */
export function DocumentEditPage({ type }: { type: DocumentType }) {
  const cfg = DOCUMENT_CONFIG[type];
  const id = Number(useParams().id) || undefined;
  const { data: doc, isPending, isError } = useDocument(cfg, id);

  if (isPending) return <Skeleton className="h-64 w-full" aria-busy />;
  if (isError || !doc) {
    return (
      <EmptyState
        title="Không tìm thấy phiếu"
        action={
          <Button asChild>
            <Link to={`/${cfg.path}`}>Về danh sách</Link>
          </Button>
        }
      />
    );
  }
  if (doc.status !== 'Draft') {
    return (
      <EmptyState
        icon={<FileLock2 />}
        title={`${doc.code} ${STATUS_LABEL[doc.status].toLowerCase()} — không sửa được`}
        description="Chỉ phiếu nháp mới sửa được. Sai sót ở phiếu đã ghi sổ thì lập phiếu điều chỉnh (kiểm kê) hoặc phiếu ngược lại."
        action={
          <Button asChild>
            <Link to={`/${cfg.path}/${doc.id}`}>Xem phiếu</Link>
          </Button>
        }
      />
    );
  }
  // key: a newer rowVersion (someone else saved) remounts the form with fresh values
  return <DocumentFormPage key={doc.rowVersion} type={type} editing={doc} />;
}
