import axios from 'axios';
import { api, cleanParams } from './api-client';
import { fileNameFromDisposition, saveBlob } from './file';

/**
 * GET a file from the API and save it. Errors come back as a Blob too (responseType: 'blob'),
 * so the ProblemDetails JSON is parsed back into `error.response.data` for the usual error toast / traceId.
 */
/** Blob.text() where available; FileReader otherwise (older engines, jsdom). */
function readBlobText(blob: Blob): Promise<string> {
  if (typeof blob.text === 'function') return blob.text();
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(reader.error);
    reader.readAsText(blob);
  });
}

export async function downloadFile(url: string, params: object = {}, fallbackName = 'download.xlsx') {
  try {
    const response = await api.get<Blob>(url, { params: cleanParams(params), responseType: 'blob' });
    saveBlob(response.data, fileNameFromDisposition(response.headers['content-disposition'] as string | undefined, fallbackName));
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.data instanceof Blob) {
      try {
        error.response.data = JSON.parse(await readBlobText(error.response.data));
      } catch {
        // not JSON: keep the Blob, the generic message is used
      }
    }
    throw error;
  }
}
