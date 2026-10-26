import { adminMessages } from '@/lib/admin/messages';
import { BlogEditor } from '../BlogEditor';

export default function NewBlogPostPage() {
  return (
    <>
      <h1 className="ui-heading-2">{adminMessages.blog.newPost}</h1>
      <BlogEditor />
    </>
  );
}
