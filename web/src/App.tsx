import { Route, Routes } from 'react-router';
import { AppShell } from './components/AppShell';
import { CardholderShell } from './components/CardholderShell';
import { CardDetailPage } from './pages/CardDetailPage';
import { CardListPage } from './pages/CardListPage';
import { PaymentRecordPage } from './pages/PaymentRecordPage';
import { CardholderViewPage } from './pages/cardholder/CardholderViewPage';
import { ReceiptPage } from './pages/cardholder/ReceiptPage';
import { ReviewPage } from './pages/cardholder/ReviewPage';

/**
 * Routes (DEC-20; 002 DEC-11). Complete in PR-00.
 * Admin (inside AppShell): `/` card list, `/cards/:id` card detail, `/cards/:id/payments/:reference` payment record.
 * Cardholder (inside CardholderShell, no login): `/cardholder/:id` view, `/cardholder/:id/pay` review,
 * `/cardholder/:id/payments/:reference` receipt.
 */
export function App() {
  return (
    <Routes>
      <Route path="/cardholder/:id" element={<CardholderShell />}>
        <Route index element={<CardholderViewPage />} />
        <Route path="pay" element={<ReviewPage />} />
        <Route path="payments/:reference" element={<ReceiptPage />} />
      </Route>
      <Route element={<AppShell />}>
        <Route path="/" element={<CardListPage />} />
        <Route path="/cards/:id" element={<CardDetailPage />} />
        <Route path="/cards/:id/payments/:reference" element={<PaymentRecordPage />} />
      </Route>
    </Routes>
  );
}
