import { ScanConfirmComponent } from './scan-confirm.component';

describe('ScanConfirmComponent.extractToken', () => {
  const guid = 'ba19bc1e-c55b-42c6-9253-3785e13c3f39';

  it('extracts the token from a kiosk deep-link URL', () => {
    const text = `http://192.168.1.2:4200/scan?token=${guid}`;
    expect(ScanConfirmComponent.extractToken(text)).toBe(guid);
  });

  it('extracts the token from an https deep-link URL', () => {
    const text = `https://192.168.1.2:4200/scan?token=${guid}`;
    expect(ScanConfirmComponent.extractToken(text)).toBe(guid);
  });

  it('accepts a bare GUID', () => {
    expect(ScanConfirmComponent.extractToken(guid)).toBe(guid);
  });

  it('accepts a GUID with surrounding whitespace', () => {
    expect(ScanConfirmComponent.extractToken(`  ${guid}  `)).toBe(guid);
  });

  it('extracts a GUID embedded in arbitrary text', () => {
    expect(ScanConfirmComponent.extractToken(`token is ${guid} ok`)).toBe(guid);
  });

  it('returns null for text with no GUID', () => {
    expect(ScanConfirmComponent.extractToken('https://example.com/hello')).toBeNull();
    expect(ScanConfirmComponent.extractToken('not a code')).toBeNull();
    expect(ScanConfirmComponent.extractToken('')).toBeNull();
  });

  it('is case-insensitive on the GUID hex', () => {
    const upper = guid.toUpperCase();
    expect(ScanConfirmComponent.extractToken(`http://x/scan?token=${upper}`)).toBe(upper);
  });
});
