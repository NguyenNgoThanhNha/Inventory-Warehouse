import { useState } from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SearchInput } from './search-input';

function Harness({ onSearch }: { onSearch: (v: string) => void }) {
  const [value, setValue] = useState('');
  return (
    <>
      <SearchInput
        aria-label="Tìm"
        value={value}
        onChange={(v) => {
          setValue(v);
          onSearch(v);
        }}
      />
      <button type="button" onClick={() => setValue('from-url')}>external</button>
    </>
  );
}

describe('SearchInput', () => {
  it('keeps focus while the debounced value round-trips, clears with the X button, follows external changes', async () => {
    const user = userEvent.setup();
    const onSearch = vi.fn();
    render(<Harness onSearch={onSearch} />);
    const input = screen.getByRole('searchbox', { name: 'Tìm' });

    await user.type(input, 'abc');
    await vi.waitFor(() => expect(onSearch).toHaveBeenLastCalledWith('abc'), { timeout: 2000 });
    expect(input).toHaveFocus(); // regression: the old input remounted (key = search) and lost focus
    await user.type(input, 'd');
    expect(input).toHaveValue('abcd');

    await user.click(screen.getByRole('button', { name: 'Xóa từ khóa' }));
    expect(input).toHaveValue('');
    expect(onSearch).toHaveBeenLastCalledWith('');

    await user.click(screen.getByRole('button', { name: 'external' }));
    expect(input).toHaveValue('from-url');
  });
});
