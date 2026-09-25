import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ColumnDef, SortingState } from '@tanstack/react-table';
import { vi } from 'vitest';
import { DataTable, pageWindow } from './data-table';

interface Row {
  id: number;
  name: string;
}

const columns: ColumnDef<Row>[] = [
  { id: 'id', header: 'ID', enableSorting: false, cell: ({ row }) => row.original.id },
  { id: 'name', header: 'Name', cell: ({ row }) => row.original.name },
];
const data: Row[] = [
  { id: 1, name: 'Alpha' },
  { id: 2, name: 'Beta' },
];

describe('DataTable', () => {
  it('pageWindow collapses long page lists with ellipses', () => {
    expect(pageWindow(1, 5)).toEqual([1, 2, 3, 4, 5]);
    expect(pageWindow(6, 12)).toEqual([1, 'ellipsis', 5, 6, 7, 'ellipsis', 12]);
    expect(pageWindow(1, 12)).toEqual([1, 2, 'ellipsis', 12]);
  });

  it('is controlled: sort clicks, page clicks, page size changes and row clicks are reported to the parent', async () => {
    const user = userEvent.setup();
    const onSortingChange = vi.fn<(s: SortingState) => void>();
    const onPageChange = vi.fn();
    const onRowClick = vi.fn();
    render(
      <DataTable
        columns={columns}
        data={data}
        sorting={[]}
        onSortingChange={onSortingChange}
        onRowClick={onRowClick}
        pagination={{ page: 1, pageSize: 20, totalCount: 45, onPageChange }}
      />,
    );

    expect(screen.getByText('Tổng: 45')).toBeInTheDocument();
    // "ID" is not sortable -> plain text header, "Name" is a sort button
    expect(screen.queryByRole('button', { name: /^ID/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Name/ }));
    expect(onSortingChange).toHaveBeenLastCalledWith([{ id: 'name', desc: false }]);

    await user.click(screen.getByRole('button', { name: 'Trang 3' }));
    expect(onPageChange).toHaveBeenLastCalledWith(3);
    expect(screen.getByRole('button', { name: 'Trang trước' })).toBeDisabled();

    await user.click(screen.getByText('Beta'));
    expect(onRowClick).toHaveBeenCalledWith({ id: 2, name: 'Beta' });
  });

  it('shows the empty text when there are no rows', () => {
    render(<DataTable columns={columns} data={[]} emptyText="Không có phiếu nào" />);
    expect(screen.getByText('Không có phiếu nào')).toBeInTheDocument();
  });
});
