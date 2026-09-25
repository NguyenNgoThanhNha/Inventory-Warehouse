import { documentSchema, emptyDocumentForm, emptyLine, findOverStock, toCreateRequest, type DocumentFormValues } from './schemas';

const p1 = { id: 1, sku: 'P001', name: 'Ốc vít M6', unit: 'cái', cost: 500 };
const p2 = { id: 2, sku: 'P002', name: 'Bản lề inox', unit: 'cái', cost: 35000 };

const form = (overrides: Partial<DocumentFormValues>): DocumentFormValues => ({ ...emptyDocumentForm(), ...overrides });
const errorPaths = (result: ReturnType<ReturnType<typeof documentSchema>['safeParse']>) =>
  result.success ? [] : result.error.issues.map((i) => i.path.join('.'));

describe('documentSchema', () => {
  it('requires type-specific header fields', () => {
    const lines = [{ ...emptyLine(), product: p1, quantity: '1' }];
    expect(errorPaths(documentSchema('GoodsReceipt').safeParse(form({ lines })))).toEqual(['warehouseId', 'supplierId']);
    expect(errorPaths(documentSchema('GoodsIssue').safeParse(form({ lines })))).toEqual(['warehouseId', 'reason']);
    expect(errorPaths(documentSchema('Transfer').safeParse(form({ lines, warehouseId: '1', toWarehouseId: '1' })))).toEqual([
      'toWarehouseId',
    ]);
    expect(documentSchema('StockTake').safeParse(form({ lines, warehouseId: '1' })).success).toBe(true);
  });

  it('rejects missing / duplicated products and non-positive quantities', () => {
    const result = documentSchema('GoodsIssue').safeParse(
      form({
        warehouseId: '1',
        reason: 'Sale',
        lines: [
          { ...emptyLine(), product: p1, quantity: '0' },
          { ...emptyLine(), product: p1, quantity: '2' },
          { ...emptyLine(), product: null, quantity: '' },
        ],
      }),
    );
    expect(errorPaths(result)).toEqual(['lines.0.quantity', 'lines.1.product', 'lines.2.product', 'lines.2.quantity']);
  });

  it('allows a counted quantity of 0 on stock takes', () => {
    const result = documentSchema('StockTake').safeParse(form({ warehouseId: '1', lines: [{ ...emptyLine(), product: p1, quantity: '0' }] }));
    expect(result.success).toBe(true);
  });
});

describe('toCreateRequest', () => {
  it('maps the transfer source warehouse to fromWarehouseId and drops unit cost outside receipts', () => {
    const values = form({
      warehouseId: '1',
      toWarehouseId: '2',
      note: '  ',
      lines: [{ product: p1, quantity: '5', unitCost: '999', note: '' }],
    });
    expect(toCreateRequest('Transfer', values, true)).toEqual({
      fromWarehouseId: 1,
      toWarehouseId: 2,
      note: null,
      lines: [{ productId: 1, quantity: 5, unitCost: null, note: null }],
      post: true,
    });
  });

  it('keeps unit cost on receipts', () => {
    const values = form({ warehouseId: '1', supplierId: '3', lines: [{ product: p2, quantity: '2', unitCost: '35000', note: 'lô 1' }] });
    expect(toCreateRequest('GoodsReceipt', values, false)).toMatchObject({
      warehouseId: 1,
      supplierId: 3,
      lines: [{ productId: 2, quantity: 2, unitCost: 35000, note: 'lô 1' }],
      post: false,
    });
  });
});

describe('findOverStock', () => {
  it('returns lines asking more than the available quantity', () => {
    const lines = [
      { ...emptyLine(), product: p1, quantity: '30' },
      { ...emptyLine(), product: p2, quantity: '10' },
      { ...emptyLine(), product: null, quantity: '99' },
    ];
    const over = findOverStock(lines, new Map([[1, 120], [2, 8]]));
    expect([...over]).toEqual([[1, 8]]);
    expect(findOverStock(lines, undefined).size).toBe(0);
  });
});
