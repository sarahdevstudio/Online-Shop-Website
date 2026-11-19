import { adminMessages } from '@/lib/admin/messages';
import { DiscountEditor } from '../DiscountEditor';

export default function NewDiscountPage() {
  return (
    <>
      <h1 className="ui-heading-2">{adminMessages.discounts.newCode}</h1>
      <DiscountEditor />
    </>
  );
}
