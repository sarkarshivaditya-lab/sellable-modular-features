package com.udc.collection.util

import android.content.Context
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Typeface
import android.graphics.pdf.PdfDocument
import com.udc.collection.domain.model.DiscountType
import com.udc.collection.domain.model.Patient
import com.udc.collection.domain.model.PaymentStatus
import dagger.hilt.android.qualifiers.ApplicationContext
import java.io.File
import java.io.FileOutputStream
import java.time.format.DateTimeFormatter
import java.util.Locale
import javax.inject.Inject
import javax.inject.Singleton

sealed class PdfResult { data class Success(val file: File) : PdfResult(); data class Error(val message: String) : PdfResult() }

@Singleton
class PdfReceiptGenerator @Inject constructor(@ApplicationContext private val context: Context) {
    private val PAGE_WIDTH = 595
    private val PAGE_HEIGHT = 842
    private val MARGIN = 40f
    private val CONTENT_WIDTH = PAGE_WIDTH - MARGIN * 2

    fun generate(customer: Patient, agentName: String): PdfResult = runCatching {
        val document = PdfDocument()
        val pageInfo = PdfDocument.PageInfo.Builder(PAGE_WIDTH, PAGE_HEIGHT, 1).create()
        val page = document.startPage(pageInfo)
        draw(page.canvas, customer, agentName)
        document.finishPage(page)
        val dir = File(context.getExternalFilesDir(null), "OMEGA6_Receipts").apply { mkdirs() }
        val file = File(dir, "Receipt_${customer.receiptNumber.replace("-", "_")}.pdf")
        FileOutputStream(file).use { document.writeTo(it) }
        document.close()
        PdfResult.Success(file)
    }.getOrElse { PdfResult.Error(it.localizedMessage ?: "PDF generation failed") }

    private fun draw(canvas: Canvas, customer: Patient, agentName: String) {
        val dateFormat = DateTimeFormatter.ofPattern("dd MMMM yyyy", Locale.ENGLISH)
        var y = MARGIN
        val headerPaint = Paint().apply { color = Color.parseColor("#1565C0"); textSize = 20f; typeface = Typeface.create(Typeface.DEFAULT, Typeface.BOLD) }
        canvas.drawText("OMEGA 6.0", MARGIN, y + 20f, headerPaint)
        val subPaint = Paint().apply { color = Color.DKGRAY; textSize = 10f }
        canvas.drawText("Universal Collection & Billing Platform", MARGIN, y + 38f, subPaint)
        y += 52f
        val rulePaint = Paint().apply { color = Color.parseColor("#1565C0"); strokeWidth = 2f }
        canvas.drawLine(MARGIN, y, MARGIN + CONTENT_WIDTH, y, rulePaint)
        val rightPaint = Paint().apply { color = Color.DKGRAY; textSize = 10f; textAlign = Paint.Align.RIGHT }
        canvas.drawText("Receipt: ${customer.receiptNumber}", MARGIN + CONTENT_WIDTH, y - 20f, rightPaint)
        canvas.drawText(customer.date.format(dateFormat), MARGIN + CONTENT_WIDTH, y - 8f, rightPaint)
        y += 18f
        val sectionPaint = Paint().apply { color = Color.parseColor("#1565C0"); textSize = 12f; typeface = Typeface.create(Typeface.DEFAULT, Typeface.BOLD) }
        canvas.drawText("CUSTOMER INFORMATION", MARGIN, y, sectionPaint)
        y += 6f
        rulePaint.strokeWidth = 0.5f
        canvas.drawLine(MARGIN, y, MARGIN + CONTENT_WIDTH, y, rulePaint)
        y += 14f
        val labelPaint = Paint().apply { color = Color.DKGRAY; textSize = 10f }
        val valuePaint = Paint().apply { color = Color.BLACK; textSize = 10f; typeface = Typeface.create(Typeface.DEFAULT, Typeface.BOLD) }
        y = drawInfoRow(canvas, y, "Customer No.", customer.patientNumber, labelPaint, valuePaint)
        y = drawInfoRow(canvas, y, "Customer Name", customer.name, labelPaint, valuePaint)
        if (customer.phone.isNotBlank()) y = drawInfoRow(canvas, y, "Phone", customer.phone, labelPaint, valuePaint)
        if (customer.address.isNotBlank()) y = drawInfoRow(canvas, y, "Address", customer.address, labelPaint, valuePaint)
        if (customer.remarks.isNotBlank()) y = drawInfoRow(canvas, y, "Notes", customer.remarks, labelPaint, valuePaint)
        y += 8f
        canvas.drawText("SERVICES", MARGIN, y, sectionPaint)
        y += 6f
        rulePaint.strokeWidth = 0.5f
        canvas.drawLine(MARGIN, y, MARGIN + CONTENT_WIDTH, y, rulePaint)
        y += 4f
        val tableHeaderPaint = Paint().apply { color = Color.WHITE; textSize = 10f; typeface = Typeface.create(Typeface.DEFAULT, Typeface.BOLD) }
        val tableRowBg = Paint().apply { color = Color.parseColor("#1565C0") }
        canvas.drawRect(MARGIN, y, MARGIN + CONTENT_WIDTH, y + 18f, tableRowBg)
        canvas.drawText("Service", MARGIN + 6f, y + 12f, tableHeaderPaint)
        tableHeaderPaint.textAlign = Paint.Align.RIGHT
        canvas.drawText("Price (₹)", MARGIN + CONTENT_WIDTH - 6f, y + 12f, tableHeaderPaint)
        y += 18f
        val rowPaint = Paint().apply { color = Color.parseColor("#F5F5F5") }
        val rowTextPaint = Paint().apply { color = Color.BLACK; textSize = 10f }
        val rowPricePaint = Paint().apply { color = Color.BLACK; textSize = 10f; textAlign = Paint.Align.RIGHT }
        customer.selectedTests.forEachIndexed { index, service ->
            if (index % 2 == 0) canvas.drawRect(MARGIN, y, MARGIN + CONTENT_WIDTH, y + 16f, rowPaint)
            val displayName = if (service.isPackage) "★ ${service.testName}" else service.testName
            canvas.drawText(displayName, MARGIN + 6f, y + 11f, rowTextPaint)
            canvas.drawText(service.price.formatCurrency(), MARGIN + CONTENT_WIDTH - 6f, y + 11f, rowPricePaint)
            y += 16f
        }
        rulePaint.strokeWidth = 0.5f
        canvas.drawLine(MARGIN, y, MARGIN + CONTENT_WIDTH, y, rulePaint)
        y += 16f
        val billingX = MARGIN + CONTENT_WIDTH * 0.55f
        val billingWidth = CONTENT_WIDTH * 0.45f
        val summaryLabelPaint = Paint().apply { color = Color.DKGRAY; textSize = 10f }
        val summaryValuePaint = Paint().apply { color = Color.BLACK; textSize = 10f; textAlign = Paint.Align.RIGHT }
        y = drawBillingRow(canvas, y, billingX, "Subtotal", customer.subtotal.formatCurrency(), summaryLabelPaint, summaryValuePaint, billingWidth)
        if (customer.discountType != DiscountType.NONE && customer.discountValue > 0) {
            val discLabel = when (customer.discountType) { DiscountType.PERCENTAGE -> "Discount (${customer.discountValue.toInt()}%)"; DiscountType.FLAT -> "Discount (Flat)"; DiscountType.NONE -> "" }
            val discAmount = when (customer.discountType) { DiscountType.PERCENTAGE -> customer.subtotal * customer.discountValue / 100.0; DiscountType.FLAT -> customer.discountValue; DiscountType.NONE -> 0.0 }
            val discPaint = Paint().apply { color = Color.RED; textSize = 10f; textAlign = Paint.Align.RIGHT }
            y = drawBillingRow(canvas, y, billingX, discLabel, "- ${discAmount.formatCurrency()}", summaryLabelPaint, discPaint, billingWidth)
        }
        val grandTotalBg = Paint().apply { color = Color.parseColor("#E3F2FD") }
        canvas.drawRect(billingX - 6f, y - 2f, billingX + billingWidth + 6f, y + 18f, grandTotalBg)
        val grandLabelPaint = Paint().apply { color = Color.parseColor("#1565C0"); textSize = 12f; typeface = Typeface.create(Typeface.DEFAULT, Typeface.BOLD) }
        val grandValuePaint = Paint().apply { color = Color.parseColor("#1565C0"); textSize = 12f; typeface = Typeface.create(Typeface.DEFAULT, Typeface.BOLD); textAlign = Paint.Align.RIGHT }
        canvas.drawText("GRAND TOTAL", billingX, y + 12f, grandLabelPaint)
        canvas.drawText(customer.grandTotal.formatCurrency(), billingX + billingWidth, y + 12f, grandValuePaint)
        y += 26f
        y += 4f
        canvas.drawText("PAYMENT DETAILS", MARGIN, y, sectionPaint)
        y += 6f
        rulePaint.strokeWidth = 0.5f
        canvas.drawLine(MARGIN, y, MARGIN + CONTENT_WIDTH, y, rulePaint)
        y += 14f
        y = drawInfoRow(canvas, y, "Payment Status", customer.paymentStatus.label, labelPaint, valuePaint.apply { color = when (customer.paymentStatus) { PaymentStatus.PAID -> Color.parseColor("#2E7D32"); PaymentStatus.UNPAID -> Color.RED; PaymentStatus.PARTIAL -> Color.parseColor("#E65100") } })
        valuePaint.color = Color.BLACK
        y = drawInfoRow(canvas, y, "Payment Method", customer.paymentMethod.label, labelPaint, valuePaint)
        if (customer.paymentStatus == PaymentStatus.PARTIAL) {
            y = drawInfoRow(canvas, y, "Amount Received", customer.amountReceived.formatCurrency(), labelPaint, valuePaint)
            y = drawInfoRow(canvas, y, "Balance Due", (customer.grandTotal - customer.amountReceived).coerceAtLeast(0.0).formatCurrency(), labelPaint, valuePaint.apply { color = Color.RED })
            valuePaint.color = Color.BLACK
        }
        val footerY = PAGE_HEIGHT - MARGIN - 50f
        rulePaint.strokeWidth = 0.5f
        canvas.drawLine(MARGIN, footerY, MARGIN + CONTENT_WIDTH, footerY, rulePaint)
        val footerPaint = Paint().apply { color = Color.DKGRAY; textSize = 9f }
        canvas.drawText("User: $agentName", MARGIN, footerY + 14f, footerPaint)
        canvas.drawText("This is a computer-generated receipt.", MARGIN, footerY + 26f, footerPaint)
        canvas.drawText("OMEGA 6.0 — Data stored locally on this device.", MARGIN, footerY + 38f, footerPaint)
        footerPaint.textAlign = Paint.Align.RIGHT
        canvas.drawText("Customer: ${customer.patientNumber}", MARGIN + CONTENT_WIDTH, footerY + 14f, footerPaint)
    }

    private fun drawInfoRow(canvas: Canvas, y: Float, label: String, value: String, labelPaint: Paint, valuePaint: Paint): Float {
        canvas.drawText("$label:", MARGIN, y, labelPaint)
        canvas.drawText(value, MARGIN + 130f, y, valuePaint)
        return y + 16f
    }

    private fun drawBillingRow(canvas: Canvas, y: Float, x: Float, label: String, value: String, labelPaint: Paint, valuePaint: Paint, width: Float): Float {
        canvas.drawText(label, x, y + 12f, labelPaint)
        canvas.drawText(value, x + width, y + 12f, valuePaint)
        return y + 16f
    }
}
