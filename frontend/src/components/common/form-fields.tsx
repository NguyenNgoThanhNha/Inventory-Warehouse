import type { ComponentProps, ReactNode } from 'react';
import type { Control, FieldPath, FieldValues } from 'react-hook-form';
import { FormControl, FormDescription, FormField, FormItem, FormLabel, FormMessage } from '@/components/ui/form';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Switch } from '@/components/ui/switch';
import { Textarea } from '@/components/ui/textarea';

interface BaseFieldProps<T extends FieldValues> {
  // any transformed output type (schemas with .transform() / coerce) — fields only touch the input side
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  control: Control<T, any, any>;
  name: FieldPath<T>;
  label: ReactNode;
  required?: boolean;
  description?: ReactNode;
}

/** Adds a red asterisk via CSS (keeps the label's accessible name clean). */
export const REQUIRED_LABEL_CLASS = "after:ml-0.5 after:text-destructive after:content-['*']";

/** RHF-bound text input with label, description and error message (shadcn Form). */
export function TextFormField<T extends FieldValues>({
  control,
  name,
  label,
  required,
  description,
  ...inputProps
}: BaseFieldProps<T> & Omit<ComponentProps<typeof Input>, 'name' | 'value' | 'onChange' | 'onBlur'>) {
  return (
    <FormField
      control={control as Control<T>}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel className={required ? REQUIRED_LABEL_CLASS : undefined}>{label}</FormLabel>
          <FormControl>
            <Input {...inputProps} {...field} value={field.value ?? ''} />
          </FormControl>
          {description && <FormDescription>{description}</FormDescription>}
          <FormMessage />
        </FormItem>
      )}
    />
  );
}

/** RHF-bound textarea with an optional character counter. */
export function TextareaFormField<T extends FieldValues>({
  control,
  name,
  label,
  required,
  description,
  maxLength,
  ...textareaProps
}: BaseFieldProps<T> & Omit<ComponentProps<typeof Textarea>, 'name' | 'value' | 'onChange' | 'onBlur'>) {
  return (
    <FormField
      control={control as Control<T>}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel className={required ? REQUIRED_LABEL_CLASS : undefined}>{label}</FormLabel>
          <FormControl>
            <Textarea {...textareaProps} {...field} value={field.value ?? ''} />
          </FormControl>
          <div className="flex justify-between gap-2">
            <div>
              {description && <FormDescription>{description}</FormDescription>}
              <FormMessage />
            </div>
            {maxLength && (
              <span className="shrink-0 text-xs text-muted-foreground">
                {String(field.value ?? '').length}/{maxLength}
              </span>
            )}
          </div>
        </FormItem>
      )}
    />
  );
}

/** RHF-bound shadcn Select. Values are strings in the DOM; the schema coerces (e.g. z.coerce.number()). */
export function SelectFormField<T extends FieldValues>({
  control,
  name,
  label,
  required,
  description,
  options,
  placeholder = 'Chọn...',
  disabled,
}: BaseFieldProps<T> & {
  options: { value: string; label: string }[];
  placeholder?: string;
  disabled?: boolean;
}) {
  return (
    <FormField
      control={control as Control<T>}
      name={name}
      render={({ field }) => (
        <FormItem>
          <FormLabel className={required ? REQUIRED_LABEL_CLASS : undefined}>{label}</FormLabel>
          <Select
            value={field.value === undefined || field.value === null || field.value === '' ? '' : String(field.value)}
            onValueChange={field.onChange}
            disabled={disabled}
          >
            <FormControl>
              <SelectTrigger className="w-full" onBlur={field.onBlur}>
                <SelectValue placeholder={placeholder} />
              </SelectTrigger>
            </FormControl>
            <SelectContent>
              {options.map((o) => (
                <SelectItem key={o.value} value={o.value}>
                  {o.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          {description && <FormDescription>{description}</FormDescription>}
          <FormMessage />
        </FormItem>
      )}
    />
  );
}

/** RHF-bound Switch rendered as a bordered row (label + description on the left). */
export function SwitchFormField<T extends FieldValues>({ control, name, label, description }: BaseFieldProps<T>) {
  return (
    <FormField
      control={control as Control<T>}
      name={name}
      render={({ field }) => (
        <FormItem className="flex flex-row items-center justify-between gap-4 rounded-md border p-3">
          <div className="space-y-0.5">
            <FormLabel>{label}</FormLabel>
            {description && <FormDescription>{description}</FormDescription>}
          </div>
          <FormControl>
            <Switch checked={!!field.value} onCheckedChange={field.onChange} />
          </FormControl>
        </FormItem>
      )}
    />
  );
}
