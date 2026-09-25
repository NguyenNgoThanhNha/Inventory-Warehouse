import { useState } from 'react';
import { render as rtlRender, screen } from '@testing-library/react';
import type { ReactElement } from 'react';
import { TooltipProvider } from '@/components/ui/tooltip';
import userEvent from '@testing-library/user-event';
import { vi } from 'vitest';
import { activities } from '@/test/fixtures';
import type { ActivityPermissionInput } from '@/types';
import { normalizePermissions, PermissionMatrix, togglePermission } from './permission-matrix';

const render = (ui: ReactElement) => rtlRender(<TooltipProvider>{ui}</TooltipProvider>);

function Harness({ initial, onChange }: { initial: ActivityPermissionInput[]; onChange: (v: ActivityPermissionInput[]) => void }) {
  const [value, setValue] = useState(initial);
  return (
    <PermissionMatrix
      activities={activities}
      value={value}
      onChange={(next) => {
        setValue(next);
        onChange(next);
      }}
    />
  );
}

describe('PermissionMatrix', () => {
  it('toggles individual C/R/U/D checkboxes and reports the new value', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <Harness
        initial={[{ activityId: 'act-issue', c: true, r: false, u: false, d: false }]}
        onChange={onChange}
      />,
    );

    const issueC = screen.getByLabelText('GOODS_ISSUE C');
    const issueR = screen.getByLabelText('GOODS_ISSUE R');
    expect(issueC).toBeChecked();
    expect(issueR).not.toBeChecked();

    await user.click(issueR);
    expect(issueR).toBeChecked();
    expect(onChange).toHaveBeenLastCalledWith([{ activityId: 'act-issue', c: true, r: true, u: false, d: false }]);

    // adds a new row for an activity that had no permissions yet
    await user.click(screen.getByLabelText('USER R'));
    expect(onChange.mock.lastCall?.[0]).toContainEqual({ activityId: 'act-user', c: false, r: true, u: false, d: false });

    await user.click(issueC);
    expect(issueC).not.toBeChecked();
    expect(onChange.mock.lastCall?.[0]).toContainEqual({ activityId: 'act-issue', c: false, r: true, u: false, d: false });
  });

  it('disables flags that do not apply to an activity (e.g. STOCK_REPORT only has R)', () => {
    render(<Harness initial={[]} onChange={() => {}} />);
    expect(screen.getByLabelText('STOCK_REPORT R')).toBeEnabled();
    expect(screen.getByLabelText('STOCK_REPORT C')).toBeDisabled();
    expect(screen.getByLabelText('USER D')).toBeDisabled();
  });

  it('"all" checkbox toggles every applicable flag of a row (indeterminate when partial)', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<Harness initial={[{ activityId: 'act-issue', c: true, r: false, u: false, d: false }]} onChange={onChange} />);
    expect(screen.getByLabelText('GOODS_ISSUE all')).toHaveAttribute('aria-checked', 'mixed');
    await user.click(screen.getByLabelText('GOODS_ISSUE all'));
    expect(onChange).toHaveBeenLastCalledWith([{ activityId: 'act-issue', c: true, r: true, u: true, d: true }]);
    expect(screen.getByLabelText('GOODS_ISSUE all')).toBeChecked();
    await user.click(screen.getByLabelText('GOODS_ISSUE all'));
    expect(onChange).toHaveBeenLastCalledWith([{ activityId: 'act-issue', c: false, r: false, u: false, d: false }]);
  });

  it('is read-only when requested', () => {
    render(
      <PermissionMatrix
        activities={activities}
        value={[{ activityId: 'act-report', c: false, r: true, u: false, d: false }]}
        readOnly
      />,
    );
    expect(screen.getByLabelText('STOCK_REPORT R')).toBeChecked();
    expect(screen.getByLabelText('STOCK_REPORT R')).toBeDisabled();
    expect(screen.queryByLabelText('STOCK_REPORT all')).not.toBeInTheDocument();
    expect(screen.queryByText('Tất cả')).not.toBeInTheDocument();
  });

  it('helpers: togglePermission is immutable and normalizePermissions drops empty rows', () => {
    const before: ActivityPermissionInput[] = [{ activityId: 'a', c: true, r: false, u: false, d: false }];
    const after = togglePermission(before, 'a', 'C', false);
    expect(before[0].c).toBe(true);
    expect(after[0].c).toBe(false);
    expect(normalizePermissions(after)).toEqual([]);
  });
});
