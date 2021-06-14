
export interface NewCardModel {
  lastName: string;
  firstName: string;
  email: string;
  cardNumber: bigint;
  cardExpiry: number;
  cvv: number;
  vault: boolean;
  amount: number;
}

export interface CardPaymentResponse {
  completed: boolean;
  secure3DHtml: string | null;
  payRequestId: string;
  response: string;
}
