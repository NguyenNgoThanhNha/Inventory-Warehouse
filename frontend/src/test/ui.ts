import { screen } from '@testing-library/react';
import type { UserEvent } from '@testing-library/user-event';

/** Opens a Radix Select (by its trigger element or accessible name) and picks an option. */
export async function chooseSelectOption(user: UserEvent, trigger: HTMLElement | string, optionName: string | RegExp) {
  const el = typeof trigger === 'string' ? screen.getByRole('combobox', { name: trigger }) : trigger;
  await user.click(el);
  await user.click(await screen.findByRole('option', { name: optionName }));
}
