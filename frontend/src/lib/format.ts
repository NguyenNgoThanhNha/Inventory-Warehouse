const qtyFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 3 });
const moneyFormat = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 0 });

/** Quantity with up to 3 decimals, vi-VN grouping: 1.234,5 */
export const formatQty = (value: number | null | undefined) => (value === null || value === undefined ? '—' : qtyFormat.format(value));

/** VND amount without decimals: 1.250.000 */
export const formatMoney = (value: number | null | undefined) =>
  value === null || value === undefined ? '—' : moneyFormat.format(value);

/** Signed quantity for differences: +3 / −2 / 0 */
export const formatSigned = (value: number | null | undefined) => {
  if (value === null || value === undefined) return '—';
  if (value === 0) return '0';
  return value > 0 ? `+${qtyFormat.format(value)}` : `−${qtyFormat.format(-value)}`;
};
