'use client';

import { useActionState } from 'react';
import { Form, FormActions, SubmitButton } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminReviewMessages as t } from '@/lib/admin/review-messages';
import type { AdminReview } from '@/lib/admin/review-types';
import { FormBanner } from '../../_components/FormBits';
import { moderateReviewAction } from './actions';

/** Approve / reject for one review: only the decisions that change something are offered. */
export function ModerateButtons({ review }: { review: AdminReview }) {
  const [state, action] = useActionState(moderateReviewAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />} inline>
      <input type="hidden" name="id" value={review.id} />
      <input type="hidden" name="rowVersion" value={review.rowVersion} />
      <FormActions>
        {review.status !== 'approved' && (
          <SubmitButton name="decision" value="approve">
            {t.approve}
          </SubmitButton>
        )}
        {review.status !== 'rejected' && (
          <SubmitButton name="decision" value="reject" variant="danger">
            {t.reject}
          </SubmitButton>
        )}
      </FormActions>
    </Form>
  );
}
