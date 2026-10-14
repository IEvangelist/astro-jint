export interface Product {
  id: string;
  name: string;
  price: number;
  description: string;
}

export const products: Product[] = [
  { id: '1', name: 'Espresso machine', price: 249, description: 'Fifteen bars of pressure and a steam wand that does not sulk.' },
  { id: '2', name: 'Burr grinder', price: 89, description: 'Forty-eight grind settings, from Turkish to cold brew.' },
  { id: '3', name: 'Gooseneck kettle', price: 45, description: 'Pours exactly where you point it.' },
];
