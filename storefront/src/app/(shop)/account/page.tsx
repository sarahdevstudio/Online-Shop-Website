import { messages } from '@/lib/messages';
import { pageMetadata } from '@/lib/seo';

export async function generateMetadata() {
  return pageMetadata({ title: messages.accountTitle, description: messages.accountTitle, path: '/account', noindex: true });
}

export default function AccountPage() {
  return (
    <div className="ui-container ui-section">
      <h1 className="ui-heading-2">{messages.accountTitle}</h1>
      <p className="ui-text-muted">{messages.accountPending}</p>
    </div>
  );
}
