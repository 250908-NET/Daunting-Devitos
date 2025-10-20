// Example test to verify Jest setup is working
describe('Jest Setup', () => {
  it('should run basic assertions', () => {
    expect(true).toBe(true);
  });

  it('should perform mathematical operations', () => {
    expect(2 + 2).toBe(4);
  });

  it('should handle arrays', () => {
    const arr = [1, 2, 3];
    expect(arr).toHaveLength(3);
    expect(arr).toContain(2);
  });

  it('should handle objects', () => {
    const obj = { name: 'Test', value: 42 };
    expect(obj).toHaveProperty('name');
    expect(obj.value).toBe(42);
  });
});
