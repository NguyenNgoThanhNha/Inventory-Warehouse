import { useCallback, useContext } from 'react';
import { UNSAFE_DataRouterContext, useBeforeUnload, useBlocker } from 'react-router-dom';
import { ConfirmDialog } from './confirm-dialog';

/**
 * Warns before losing unsaved form input: a confirm dialog for in-app navigation (needs a data router —
 * the app uses createBrowserRouter; plain routers, e.g. in unit tests, only get the tab-close warning) and
 * the browser's own prompt when closing / reloading the tab.
 * `shouldBlock` is read at navigation time, so a save that navigates right away is never blocked.
 */
export function UnsavedChangesGuard({ shouldBlock }: { shouldBlock: () => boolean }) {
  const dataRouter = useContext(UNSAFE_DataRouterContext);
  useBeforeUnload(
    useCallback(
      (e: BeforeUnloadEvent) => {
        if (shouldBlock()) e.preventDefault();
      },
      [shouldBlock],
    ),
  );
  return dataRouter ? <NavigationBlocker shouldBlock={shouldBlock} /> : null;
}

function NavigationBlocker({ shouldBlock }: { shouldBlock: () => boolean }) {
  const blocker = useBlocker(({ currentLocation, nextLocation }) => currentLocation.pathname !== nextLocation.pathname && shouldBlock());
  return (
    <ConfirmDialog
      open={blocker.state === 'blocked'}
      onOpenChange={(open) => {
        if (!open && blocker.state === 'blocked') blocker.reset();
      }}
      title="Rời trang khi chưa lưu?"
      description="Các thay đổi trong phiếu này sẽ mất."
      confirmText="Rời trang"
      cancelText="Ở lại"
      destructive
      onConfirm={() => blocker.proceed?.()}
    />
  );
}
